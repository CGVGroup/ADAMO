from __future__ import annotations

from typing import ClassVar
from pydantic import BaseModel, Field
from langchain_core.tools import BaseTool
import httpx


from typing import ClassVar, List, Dict
from pydantic import BaseModel, Field, ConfigDict, field_validator
from langchain_core.tools import BaseTool
import ast
import operator as _op


# ============================================================
# SOMMA: somma una lista di numeri
# ============================================================
class SumTool(BaseTool):
    """
    Somma una lista di numeri.

    - Esempio:
        {"numbers": [1, 2.5, 3]} -> 6.5
    """

    # --- Schema degli argomenti annidato nella classe ---
    class Args(BaseModel):
        numbers: List[float] = Field(
            ...,
            description="Lista di numeri da sommare."
        )

    # --- Metadati tool (ClassVar: non diventano campi Pydantic) ---
    name: ClassVar[str] = "math_sum"
    description: ClassVar[str] = "Somma una lista di numeri e restituisce il totale."
    args_schema: ClassVar[type[BaseModel]] = Args

    # --- Logica sincrona del tool ---
    def _run(self, numbers: List[float]) -> dict:
        return {"operation": "sum", "input": numbers, "result": float(sum(numbers))}

    # --- Variante asincrona (facoltativa) ---
    async def _arun(self, numbers: List[float]) -> dict:
        return self._run(numbers)


# ============================================================
# SOTTRAZIONE: a - b
# ============================================================
class SubTool(BaseTool):
    """
    Calcola a - b.
    """

    class Args(BaseModel):
        a: float = Field(..., description="Minuendo.")
        b: float = Field(..., description="Sottraendo.")

    name: ClassVar[str] = "math_sub"
    description: ClassVar[str] = "Sottrae b da a (a - b)."
    args_schema: ClassVar[type[BaseModel]] = Args

    def _run(self, a: float, b: float) -> dict:
        return {"operation": "sub", "input": {"a": a, "b": b}, "result": float(a - b)}

    async def _arun(self, a: float, b: float) -> dict:
        return self._run(a, b)


# ============================================================
# MOLTIPLICAZIONE: prodotto di una lista di numeri
# ============================================================
class MulTool(BaseTool):
    """
    Moltiplica una lista di numeri.
    """

    class Args(BaseModel):
        numbers: List[float] = Field(
            ...,
            description="Lista di numeri da moltiplicare."
        )

    name: ClassVar[str] = "math_mul"
    description: ClassVar[str] = "Moltiplica una lista di numeri e restituisce il prodotto."
    args_schema: ClassVar[type[BaseModel]] = Args

    def _run(self, numbers: List[float]) -> dict:
        prod = 1.0
        for x in numbers:
            prod *= float(x)
        return {"operation": "mul", "input": numbers, "result": float(prod)}

    async def _arun(self, numbers: List[float]) -> dict:
        return self._run(numbers)


# ============================================================
# DIVISIONE: a / b (con controllo divisione per zero)
# ============================================================
class DivTool(BaseTool):
    """
    Divide numerator per denominator (numerator / denominator).
    Solleva ZeroDivisionError se denominator == 0.
    """

    class Args(BaseModel):
        numerator: float = Field(..., description="Numeratore.")
        denominator: float = Field(..., description="Denominatore (≠ 0).")

    name: ClassVar[str] = "math_div"
    description: ClassVar[str] = "Divide numerator per denominator (numerator / denominator)."
    args_schema: ClassVar[type[BaseModel]] = Args

    def _run(self, numerator: float, denominator: float) -> dict:
        if denominator == 0:
            raise ZeroDivisionError("Denominator must be non-zero.")
        return {
            "operation": "div",
            "input": {"numerator": numerator, "denominator": denominator},
            "result": float(numerator / denominator),
        }

    async def _arun(self, numerator: float, denominator: float) -> dict:
        return self._run(numerator, denominator)


# ============================================================
# POTENZA: base ** exponent
# ============================================================
class PowTool(BaseTool):
    """
    Eleva la base all'esponente (base ** exponent).
    """

    class Args(BaseModel):
        base: float = Field(..., description="Base.")
        exponent: float = Field(..., description="Esponente.")

    name: ClassVar[str] = "math_pow"
    description: ClassVar[str] = "Eleva la base all'esponente (base ** exponent)."
    args_schema: ClassVar[type[BaseModel]] = Args

    def _run(self, base: float, exponent: float) -> dict:
        return {"operation": "pow", "input": {"base": base, "exponent": exponent}, "result": float(base ** exponent)}

    async def _arun(self, base: float, exponent: float) -> dict:
        return self._run(base, exponent)


# ============================================================
# VALUTATORE DI ESPRESSIONI SICURO (subset aritmetico)
# - Supporta: + - * / // % **, parentesi, numeri, variabili fornite
# - Blocca:    qualunque altra struttura (Call, Attribute, Subscript, ecc.)
# ============================================================

# Operatori ammessi (AST node -> funzione Python)
_ALLOWED_OPS = {
ast.Add: _op.add,
ast.Sub: _op.sub,
ast.Mult: _op.mul,
ast.Div: _op.truediv,
ast.FloorDiv: _op.floordiv,
ast.Mod: _op.mod,
ast.Pow: _op.pow,
ast.USub: _op.neg,
ast.UAdd: _op.pos,
}

class EvalExprTool(BaseTool):
    """
    Valuta un'espressione aritmetica in modo sicuro usando AST.

    Esempi:
      - expression="3*(x+2) - y/5", vars={"x": 4, "y": 10}
      - expression="2**8 + 5%3"
    """

   

    class Args(BaseModel):
        expression: str = Field(..., description="Espressione, es. '3*(x+2) - y/5'.")
        vars: Dict[str, float] = Field(
            default_factory=dict,
            description="Mappa variabili -> valore (float)."
        )

        # Impedisci campi extra non dichiarati
        model_config = ConfigDict(extra="forbid")

        # Normalizza/valida l'espressione
        @field_validator("expression")
        @classmethod
        def _strip_expr(cls, v: str) -> str:
            v = v.strip()
            if not v:
                raise ValueError("Expression must be non-empty.")
            return v

    name: ClassVar[str] = "math_eval"
    description: ClassVar[str] = (
        "Valuta un'espressione aritmetica sicura con operatori (+, -, *, /, //, %, **), "
        "parentesi e variabili fornite in 'vars'."
    )
    args_schema: ClassVar[type[BaseModel]] = Args



    # --- Valutatore ricorsivo sull'AST ---
    def _eval_node(self, node: ast.AST, variables: Dict[str, float]) -> float:
        # Nodo radice per `mode="eval"`
        if isinstance(node, ast.Expression):
            return self._eval_node(node.body, variables)

        # Numeri
        if isinstance(node, ast.Num):  # compat vecchie versioni Python
            return float(node.n)

        if isinstance(node, ast.Constant):
            if isinstance(node.value, (int, float)):
                return float(node.value)
            raise ValueError(f"Unsupported constant type: {type(node.value).__name__}")

        # Variabili
        if isinstance(node, ast.Name):
            if node.id not in variables:
                raise NameError(f"Undefined variable: {node.id}")
            return float(variables[node.id])

        # Operazioni binarie
        if isinstance(node, ast.BinOp):
            left = self._eval_node(node.left, variables)
            right = self._eval_node(node.right, variables)
            op_type = type(node.op)
            if op_type not in _ALLOWED_OPS:
                raise ValueError(f"Operator not allowed: {op_type.__name__}")
            if op_type is ast.Div and right == 0:
                raise ZeroDivisionError("Division by zero.")
            return float(_ALLOWED_OPS[op_type](left, right))

        # Operatori unari (+x, -x)
        if isinstance(node, ast.UnaryOp):
            operand = self._eval_node(node.operand, variables)
            op_type = type(node.op)
            if op_type not in _ALLOWED_OPS:
                raise ValueError(f"Unary operator not allowed: {op_type.__name__}")
            return float(_ALLOWED_OPS[op_type](operand))

        # Tutto il resto è vietato (Call/Attribute/Subscript/Comprehension/etc.)
        raise ValueError(f"Node not allowed in expression: {type(node).__name__}")

    def _run(self, expression: str, vars: Dict[str, float] | None = None) -> dict:
        vars = vars or {}
        # Parsing sicuro: ast.parse con mode="eval" (no statement)
        try:
            tree = ast.parse(expression, mode="eval")
        except SyntaxError as e:
            raise ValueError(f"Syntax error in expression: {e}") from e

        result = self._eval_node(tree, vars)
        return {"operation": "eval", "input": {"expression": expression, "vars": vars}, "result": float(result)}

    async def _arun(self, expression: str, vars: Dict[str, float] | None = None) -> dict:
        return self._run(expression, vars)


#API/search tools

class WeatherTool(BaseTool):
    """Ottiene il meteo corrente e una previsione di 3 giorni per una località."""

    class WeatherArgs(BaseModel):
        location: str = Field(..., description="Nome della località (es. 'Roma', 'New York').")

    # Questi devono essere ClassVar
    name: ClassVar[str] = "get_weather"
    description: ClassVar[str] = "Restituisce il meteo corrente e la previsione per i prossimi 3 giorni di una località."
    args_schema: ClassVar[type[BaseModel]] = WeatherArgs

    def _run(self, location: str) -> dict:
        # 1) Geocoding
        geo_url = "https://geocoding-api.open-meteo.com/v1/search"
        with httpx.Client(timeout=10) as client:
            r = client.get(geo_url, params={"name": location, "count": 1})
            r.raise_for_status()
            geo = r.json()
        results = geo.get("results") or []
        if not results:
            raise ValueError(f"Località non trovata: {location}")
        loc = results[0]
        lat, lon = loc["latitude"], loc["longitude"]

        # 2) Forecast
        forecast_url = "https://api.open-meteo.com/v1/forecast"
        params = {
            "latitude": lat,
            "longitude": lon,
            "current": "temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code",
            "daily": "temperature_2m_max,temperature_2m_min,precipitation_sum",
            "forecast_days": 3,
            "timezone": "auto",
        }
        with httpx.Client(timeout=10) as client:
            r = client.get(forecast_url, params=params)
            r.raise_for_status()
            forecast = r.json()

        return {
            "resolved_location": loc,
            "forecast": forecast,
        }

    async def _arun(self, location: str) -> dict:
        return self._run(location)
