using System;
using System.Collections.Generic;
using UnityEngine;

public enum Stage { Start, Prepare, Interact, Quiz, Complete }

/// <summary>Holds the game state: current stage, discovered items, points, quiz result.</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public event Action Changed;                       // fired whenever anything changes

    public Stage CurrentStage { get; private set; } = Stage.Start;
    public int Points { get; private set; }
    public int FoundCount { get; private set; }
    public int QuizCorrect { get; private set; }
    public bool QuizDone { get; private set; }

    bool[] found;

    public int ItemCount => GoBagData.Items.Length;
    public bool AllFound => FoundCount >= ItemCount;
    public int MaxPoints => ItemCount * GoBagData.PointsPerItem + GoBagData.Quiz.Length * GoBagData.PointsPerAnswer;
    public float Percent => Mathf.Clamp01(Points / (float)MaxPoints);

    void Awake()
    {
        Instance = this;
        found = new bool[ItemCount];
    }

    public bool IsFound(int i) => found[i];

    public void SetStage(Stage s) { CurrentStage = s; Changed?.Invoke(); }

    /// <summary>Returns true if this was a NEW discovery (points awarded).</summary>
    public bool Discover(int i)
    {
        if (found[i]) return false;
        found[i] = true; FoundCount++; Points += GoBagData.PointsPerItem;
        Changed?.Invoke();
        return true;
    }

    public void AnswerQuiz(bool correct)
    {
        if (!correct) return;
        QuizCorrect++; Points += GoBagData.PointsPerAnswer;
        Changed?.Invoke();
    }

    public void FinishQuiz() { QuizDone = true; SetStage(Stage.Complete); }

    public void ResetGame()
    {
        found = new bool[ItemCount];
        Points = FoundCount = QuizCorrect = 0;
        QuizDone = false;
        SetStage(Stage.Start);
    }

    public string Rank
    {
        get
        {
            if (Percent >= 0.9f) return "Ready for Anything!";
            if (Percent >= 0.6f) return "Prepared Pro";
            if (Percent >= 0.3f) return "Getting Ready";
            return "Beginner";
        }
    }

    public List<string> Achievements()
    {
        var list = new List<string>();
        if (FoundCount >= 1) list.Add("First Find - discovered your first item");
        if (AllFound) list.Add("Explorer - discovered every item");
        if (QuizDone && QuizCorrect == GoBagData.Quiz.Length) list.Add("Quiz Ace - perfect quiz score");
        if (Percent >= 0.9f) list.Add("Go-Bag Master - 90%+ completion");
        return list;
    }
}
