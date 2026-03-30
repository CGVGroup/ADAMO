#!/usr/bin/env python3
"""
Usage Example:
--------------
python run_experiments.py --parallelism 1 --exe "C:/path/to/your_executable.exe" --agent "adam_python/adam_agent_server.py" --csv "adam_unity/Assets/BenchmarkData/runs.csv" --benchmark-root "adam_unity/Assets/BenchmarkData/" --batch-root "./" --exp-dir "adam_experiments/placeholder" --timescale 5


Arguments:
----------

--parallelism, -p (int, default=1)
    Maximum number of processes to run in parallel.
    Must be >=1 and <= number of runs in the CSV file.

--exe (Path, default="C:/path/to/your_executable.exe")
    Path to the external executable that will be launched for each batch of runs.
    This is the main application consuming the CSV data.

--agent (Path, default="adam_python/adam_agent_server.py")
    Path to the agent script.
    Not used directly by the runner, but kept as a configurable parameter.

--csv (Path, default="adam_unity/Assets/BenchmarkData/runs.csv")
    Input CSV file containing the list of runs.
    Each row corresponds to one run.

--benchmark-root (Path, default="adam_unity/Assets/BenchmarkData/")
    Root directory containing benchmark-related files.
    Also passed to `analyze_experiment` for analysis and aggregation.

--batch-root (Path, default="./")
    Directory where the batched CSV files will be saved.
    Each batch is a partition of the original CSV (e.g. runs_p0.csv, runs_p1.csv, ...).

--exp-dir (Path, default="adam_experiments/placeholder")
    Directory where results and metrics from the runs will be copied.
    Used by `analyze_experiment` to produce reports.

--timescale (int, default=5)
    Time scaling factor for the simulations.
    1 = real time; higher values speed up execution.

--exp-name (str, default=EXP_NAME)
"""

import sys
sys.path.append("./adam_experiments")
import md_utils

import argparse
from argparse import Namespace
import asyncio
from pathlib import Path
import pandas as pd
from typing import List, Union, Dict, Any
from tqdm.asyncio import tqdm_asyncio  # tqdm >= 4.66
from adam_experiments.experiment_utils import copy_folder,compute_experiment_result

from adam_python.agentic_forge.checkpointers import SqliteSaver,forge_checkpointer,CheckpointerConfig

# =========================
# Defaults
# =========================
BENCHMARK_PATH = Path("./adam_unity/Build/ADAMO_Build_Data/BenchmarkData") #da qui pesco le cartelle dei run e tutti i dati che mi copio
CSV_PATH = Path("./adam_unity/Build/ADAMO_Build_Data/BenchmarkData/runs.csv") #da qui pesco il csv con tutte le run 
AGENT_APP_PATH = Path("./adam_python/adam_agent_server.py").absolute() #Questo è il path del server uvicorn
EXE_PATH = Path("./adam_unity/Build/ADAMO_Build.exe") #path della build unity

PYTHON_PATH = Path(".venv/Scripts/python.exe").absolute()

BATCH_ROOT_PATH = Path("./adam_unity/Build/ADAMO_Build_Data/") #Qui salvo i csv splitatti delle run
EXP_DIR_PATH = Path("./adam_experiments/experiments") #Qui salvo i risultati con le metriche dell esperimento
EXP_NAME=Path("exp_default")
EP_DEF_YAML=Path("./episodes_definition.yaml")

CHECKPOINTER_DB_PATH=Path("./adam_python/checkpoints/checkpoint.db")
CHECKPOINTER_CONF_PATH=Path("./adam_python/agentic_forge/configs/checkpointer_config.yaml")

STARTING_AGENT_PORT = 50000
STARTING_TOOL_PORT = 60000
TIMESCALE = 5


async def run_shell_async(cmdline: str) -> int:
    """
    Launch a shell command asynchronously.
    - stdout/stderr inherit the parent console (no piping).
    - Returns the child's return code (0=success, >0=error).
    """
    proc = await asyncio.create_subprocess_shell(cmdline)
    return await proc.wait()


def get_args() -> Namespace:
    """
    Parse CLI arguments and print them for traceability.
    """
    parser = argparse.ArgumentParser(
        description="Simple runner with parallelism and an external command per CSV run."
    )
    parser.add_argument(
        "--parallelism", "-p", type=int, default=1,
        help="Maximum number of concurrent processes (>=1)."
    )
    parser.add_argument(
        "--exe", default=EXE_PATH,
        help=f"Executable to run for each batch (default: {EXE_PATH})."
    )
    parser.add_argument(
        "--python-exe", default=PYTHON_PATH,
        help=f"Executable of the python in virtual environment (default: {PYTHON_PATH})."
    )
    parser.add_argument(
        "--agent-app", type=Path, default=AGENT_APP_PATH,
        help=f"Agent path (default: {AGENT_APP_PATH})."
    )
    parser.add_argument(
        "--csv", type=Path, default=CSV_PATH,
        help=f"CSV with runs (default: {CSV_PATH})."
    )
    parser.add_argument(
        "--benchmark-root", type=Path, default=BENCHMARK_PATH,
        help=f"Benchmark root directory (default: {BENCHMARK_PATH})."
    )
    parser.add_argument(
        "--batch-root", type=Path, default=BATCH_ROOT_PATH,
        help=f"Output folder for batched CSVs (default: {BATCH_ROOT_PATH})."
    )
    parser.add_argument(
        "--exp-dir", type=Path, default=EXP_DIR_PATH,
        help=f"Directory to copy experiment metrics/results (default: {EXP_DIR_PATH})."
    )
    parser.add_argument(
        "--exp-name",type=Path,default=EXP_NAME,
        help=f"The name of the experiment, used to create folders (default {EXP_NAME})"
    )
    parser.add_argument(
        "--timescale", type=int, default=TIMESCALE,
        help=f"Time multiplier for faster episodes; 1 = realtime (default: {TIMESCALE})."
    )

    args = parser.parse_args()
    print(f"[ARGUMENTS] {args}")
    return args


def checks(parallelism: int, num_runs: int):
    """
    Validate requested parallelism against CSV size.
    - parallelism must be >= 1
    - CSV must contain at least 1 row
    - parallelism must be <= num_runs  (1 process per batch, up to one batch per row)
    """
    if parallelism < 1:
        raise Exception("--parallelism cannot be < 1")
    if num_runs == 0:
        raise Exception("The number of runs is 0 (empty CSV).")
    if parallelism > num_runs:
        raise Exception(f"--parallelism is {parallelism} which is > number of runs in the CSV ({num_runs})")
    return


def get_batches(
    csv_path: Union[str, Path],
    parallelism: int,
    batch_root_path: Path= BATCH_ROOT_PATH,
    exe_path: Path = EXE_PATH,
    python_exe_path: Path= PYTHON_PATH,
    agent_app_path:Path=AGENT_APP_PATH,
    timescale: int = TIMESCALE,
    exp_name:Path=EXP_NAME,
    reset_index: bool = False
) -> List[Dict[str, Any]]:
    """
    Read a CSV and split it into N batches for parallel processing.
    Returns a list of dicts: [{"name": "<stem>_p0", "df": chunk0, ...}, ...].

    Semantics:
      - N = parallelism (validated so that 1 <= N <= len(df))
      - Batches are as equal as possible; order preserved
      - If the CSV is empty, raises in checks()

    Args:
        csv_path: Path to the CSV file.
        parallelism: Number of batches to create (1..len(df)).
        batch_root_path: Folder where batch CSVs will be written.
        exe_path: Executable path to embed into the per-batch command line.
        timescale: Time multiplier to pass to the executable.
        reset_index: If True, reset each chunk index to 0..k-1.

    Returns:
        List[Dict[str, Any]] with keys:
          - "name": f"{<csv_stem>}_p<i>"
          - "df":   pandas DataFrame for that batch (copy)
          - "path": CSV path for this batch
          - "agent_port": starting base + i
          - "tool_port": starting base + i
          - "cmdline": command line string to execute
    """
    csv_path = Path(csv_path)
    df = pd.read_csv(csv_path, sep=';')
    n_rows = len(df)

    print(f"[INFO] Loaded {n_rows} runs from {csv_path}")
    checks(parallelism, n_rows)

    # After checks, we can use N = parallelism directly (1..n_rows)
    n = int(parallelism)

    # Compute balanced partitions preserving order
    q, r = divmod(n_rows, n)  # first r chunks get (q+1), others get q
    stem = csv_path.stem

    batches: List[Dict[str, Any]] = []
    start = 0
    for i in range(n):
        size = q + 1 if i < r else q
        end = start + size
        chunk = df.iloc[start:end]
        if reset_index:
            chunk = chunk.reset_index(drop=True)

        name = f"{stem}_p{i}"
        path = f"{name}.csv"
        agent_port = STARTING_AGENT_PORT + i
        tool_port = STARTING_TOOL_PORT + i
        cmdline = f"{exe_path} --agent-app=\"{agent_app_path}\" --python-exe=\"{python_exe_path}\" --runs-path=\"{path}\" --agent-port={agent_port} --tool-port={tool_port} --timescale={timescale} --exp-name={exp_name} --parallelism={parallelism}"

        batches.append({
            "name": name,
            "df": chunk.copy(),
            "path": f"{batch_root_path / Path(name)}.csv",
            "agent_port": agent_port,
            "tool_port": tool_port,
            "cmdline": cmdline
        })
        start = end


    print(f"[INFO] Split into {len(batches)} batch(es):\n")
    for batch in batches:
        batch["df"].to_csv(batch["path"], index=False, sep=';')
        print(f"\tBatch {batch['name']}) has {len(batch['df'])} runs.\n\t\tCMDLINE: {batch['cmdline']}\n")
    print("\n")
    return batches


async def run_parallel(batches: List[Dict[str, Any]]) -> int:
    """
    Lancia tutti i batch in parallelo, riportando il nome del batch quando
    ciascun processo termina (OK/FAIL) o solleva eccezioni.
    Ritorna 0 se tutti i batch hanno exit code 0, altrimenti 1.
    """
    total = len(batches)
    print(f"\n[INFO] Starting {total} batch(es) in parallel...")

    async def _run_batch(batch: Dict[str, Any]):
        """
        Incapsula l'esecuzione di UN batch restituendo (name, return_code, err).
        err è None se il processo è partito correttamente e ha terminato;
        altrimenti contiene l'eccezione sollevata.
        """
        name = batch["name"]
        cmd = batch["cmdline"]
        print(f"\n[START] {name}  ->  {cmd}\n")
        try:
            rc = await run_shell_async(cmd)
            return name, rc, None
        except Exception as e:
            return name, None, e
        
    # async def _dummy(batch: Dict[str, Any]):
    #     """
    #     Dummy task for testing purposes.
    #     """
    #     name = batch["name"]
    #     print(f"[DUMMY] {name}: This is a dummy task.")
    #     await asyncio.sleep(1)  # Simulate some async work
    #     return name, 0, None

    # crea i task "nominati"
    tasks = [asyncio.create_task(_run_batch(b)) for b in batches]
    #tasks = [asyncio.create_task(_dummy(b)) for b in batches]
    all_ok = True

    from tqdm.asyncio import tqdm_asyncio  # safety-import locale
    with tqdm_asyncio(total=total, desc="Running batches", unit="batch") as pbar:
        for fut in asyncio.as_completed(tasks):
            name, rc, err = await fut
            if err is not None:
                print(f"[ERROR] {name}: raised exception -> {err!r}")
                all_ok = False
            else:
                if rc == 0:
                    print(f"[DONE] {name}: exit code {rc}")
                else:
                    print(f"[FAIL] {name}: exit code {rc}")
                    all_ok = False
            pbar.update(1)

    return 0 if all_ok else 1


async def main():
    """
    Orchestrates:
      1) parse args
      2) build batches (1..N as per --parallelism)
      3) run all batches in parallel
      4) summarize & analyze results
    """
    args = get_args()
    batches = get_batches(csv_path=args.csv,parallelism= args.parallelism,batch_root_path= args.batch_root,
                          exe_path= args.exe,python_exe_path=args.python_exe,agent_app_path=args.agent_app,
                          timescale= args.timescale,exp_name=args.exp_name) #Metto io il time stamp o unity, meglio unity cosi lo mette anche epr al current run e io non lo gestisco
    exit_code = await run_parallel(batches)

    if exit_code == 0:
        print("\n[TOTAL RESULT] ✅ All runs completed successfully.")
        print(f"[INFO] Computing metrics in: {args.exp_dir}")
        try:
            data_folder=args.benchmark_root/args.exp_name
            exp_folder=args.exp_dir/args.exp_name
            #Capire se devo mettere il timestamp o lo emtte unity, verificare tutti i path correttamente e fomralizzare il ntoebook per l analisi di current run
            exp_folder=copy_folder(src=data_folder,
                        dest=exp_folder,timestamp_fmt=None)  #la cartella viene creata da unity, il timestamp lho già messo io o lo mette unity! Ha senso che lo emtte unity cosi lo emtte anche in current run e non cambia nulla qui
            
            cfg=CheckpointerConfig.from_yaml(CHECKPOINTER_CONF_PATH)
            cfg.config.path=Path(CHECKPOINTER_DB_PATH)
            saver:SqliteSaver=forge_checkpointer(cfg)
            
            compute_experiment_result(ep_def_yaml=EP_DEF_YAML,saver=saver,exp_folder=exp_folder)
            # analyze_experiment(
            #     mode="copy_and_compute",
            #     exp_dir=args.exp_dir,
            #     run_file_name=args.csv,
            #     bench_dir=args.benchmark_root
            # )
            # print(f"[INFO] Generating pdf report...")
            # generate_pdf_report(
            #     exp_dir=args.exp_dir,
            #     output_pdf=args.exp_dir/Path(f"{args.exp_dir.name}_report.pdf"),
            #     run_file_name=args.csv,
            #     rows_per_page=20,
            #     max_cols_per_page=20
            # )
        except Exception as e:
            # Re-raise to surface post-run analysis failures
            raise e
    else:
        print("\n[TOTAL RESULT] ❌ Some runs failed. Check logs above.")
    raise SystemExit(exit_code)


if __name__ == "__main__":
    asyncio.run(main())
