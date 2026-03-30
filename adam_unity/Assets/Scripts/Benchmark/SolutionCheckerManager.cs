// SolutionCheckerManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

/// <summary>
/// A singleton that signals the completion of a single benchmark experiment.
/// The BenchmarkManager listens to its OnExperimentCompleted event.
/// </summary>
public class SolutionCheckerManager : MonoBehaviour
{
    public static SolutionCheckerManager Instance { get; private set; }
    
    [SerializeField] private List<ObjectTag> objectTags = new List<ObjectTag>();

    [SerializeField] private SolutionCheckerBase solutionChecker;
    public SolutionCheckerBase SolutionCheker => solutionChecker;

    [Header("Task Definitions (Checkers Parameters)")]
    [SerializeField] private PickCheckerParams Task1;
    [SerializeField] private PickCheckerParams Task2;
    [SerializeField] private PickCheckerParams Task3;
    [SerializeField] private FloorCheckerParams Task4;
    [SerializeField] private ProximityCheckerParams Task5;
    [SerializeField] private FloorCheckerParams Task6;
    [SerializeField] private AreaCheckerParams Task7;
    [SerializeField] private FloorCheckerParams Task8;
    [SerializeField] private ProximityCheckerParams Task9;
    [SerializeField] private ProximityCheckerParams Task10;
    [SerializeField] private ProximityCheckerParams Task11;
    [SerializeField] private FloorCheckerParams Task12;
    [SerializeField] private ProximityCheckerParams Task13;
    [SerializeField] private FloorCheckerParams Task14;
    [SerializeField] private FloorCheckerParams Task15;

    /// <summary>
    /// Fired when an experiment is completed.
    /// </summary>
    // public static event Action OnTaskSolved;

    private void Awake()
    {
        // Enforce singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetupSolutionChecker()
    {
        objectTags = new List<ObjectTag>(FindObjectsByType<ObjectTag>(FindObjectsSortMode.None));
        
        solutionChecker = BenchmarkManager.Instance.CurrentRun.solutionChecker switch
        {
            SolutionCheckerType.C1 => this.gameObject.AddComponent<ProximityChecker>(),
            SolutionCheckerType.C2 => this.gameObject.AddComponent<FloorChecker>(),
            SolutionCheckerType.C3 => this.gameObject.AddComponent<PickChecker>(),
            SolutionCheckerType.C4 => this.gameObject.AddComponent<AreaChecker>(),
            SolutionCheckerType.C5 => this.gameObject.AddComponent<CircularLayoutChecker>(),
            _ => null
        };
        Assert.IsNotNull(solutionChecker, $"Invalid SolutionChecker {BenchmarkManager.Instance.CurrentRun.solutionChecker}");
        
        // Setup Checker Parameters (any type)
        SolutionCheckerParams taskCheckerParams = BenchmarkManager.Instance.CurrentRun.taskId switch
        {
            TaskId.T1 => Task1,
            TaskId.T2 => Task2,
            TaskId.T3 => Task3,
            TaskId.T4 => Task4,
            TaskId.T5 => Task5,
            TaskId.T6 => Task6,
            TaskId.T7 => Task7,
            TaskId.T8 => Task8,
            TaskId.T9 => Task9,
            TaskId.T10 => Task10,
            TaskId.T11 => Task11,
            TaskId.T12 => Task12,
            TaskId.T13 => Task13,
            TaskId.T14 => Task14,
            TaskId.T15 => Task15,
            _ => null,
        };
        Assert.IsNotNull(taskCheckerParams, $"Invalid TaskID {BenchmarkManager.Instance.CurrentRun.taskId}");
        try
        {
            StartCoroutine(solutionChecker.SetupCheckerData(taskCheckerParams));
        }
        catch (AssertionException e)
        {
            Debug.LogError($"Possible mismatch between Task and Checker: " +
                           $"{BenchmarkManager.Instance.CurrentRun.taskId}-" +
                           $"{BenchmarkManager.Instance.CurrentRun.solutionChecker} " +
                           $"could not be a valid Run" +
                           $"\n{e.Message}");
        }
    }

     void Update()
     {
         if(solutionChecker == null)
             return;

         solutionChecker.CheckCompletion(); 
         // Called here on Update() only for Completion calculation debug and 3D View Debugging purposes
     }

     public ObjectTag GetObjTagFromId(int id)
     {
         foreach (var obj in objectTags)
             if (obj.PersistentId == id)
                 return obj;
         
         Debug.LogError($"ObjectTag with ID {id} not found");
         return null;
     }
}