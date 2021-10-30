using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class LockgridScript : MonoBehaviour {

    enum ArrowDir
    {
        Up,
        Right,
        Down,
        Left
    }
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;

    public KMSelectable[] buttons;
    public KMSelectable resetButton;
    public MeshRenderer[] rings;
    public MeshRenderer statusRing;
    public Material unlit, lit;

    private Transform[] arrowTFs = new Transform[16];
    private static readonly string[] coords = { "A1", "B1", "C1", "D1", "A2", "B2", "C2", "D2", "A3", "B3", "C3", "D3", "A4", "B4", "C4", "D4" };

    private bool[] pressed = new bool[16];
    private bool[] animating = new bool[16];
    private ArrowDir[] initGrid = new ArrowDir[16];
    private ArrowDir[] dispGrid = new ArrowDir[16];
    private int[] queues = Enumerable.Repeat(0, 16).ToArray();
    int prevPress = -1;
    private bool resetting;

    List<int> generationPresses;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake () {
        moduleId = moduleIdCounter++;
        for (int i = 0; i < 16; i++)
        {
            int ix = i;
            arrowTFs[ix] = buttons[ix].transform;
            buttons[ix].OnInteract += delegate () { Press(ix); return false; };
        }
        resetButton.OnInteract += delegate () { StartCoroutine( Reset() );  return false; };
    }

    void Start ()
    {
        GeneratePuzzle();
        for (int i = 0; i < 16; i++)
        {
            dispGrid[i] = initGrid[i];
            arrowTFs[i].localEulerAngles = GetVector(initGrid[i]);
        }
        Ut.LogIntegerGrid("Lockgrid", moduleId, dispGrid.Cast<int>().ToArray(), 4, 4, "↑→↓←".ToCharArray());
        Log("One solution to the puzzle is: {0}.", generationPresses.Select(x => coords[x]).Join(", "));
    }
    void GeneratePuzzle()
    {
        Restart:
        List<int> presses = new List<int>();
        bool[] pressed = new bool[16];
        do
        {
            for (int i = 0; i < 16; i++)
                initGrid[i] = (ArrowDir)Rnd.Range(0, 4);
            if (AnySelfPoints(initGrid))
                goto Restart;
            int[] safe = Enumerable.Range(0, 16).Where(x => IsSafe(x, initGrid)).ToArray();
            int[] safeAndUntouched = safe.Where(x => !pressed[x]).ToArray();
            int taken;
            if (safe.Length == 0)
                goto Restart;
            if (safeAndUntouched.Length != 0)
                taken = safeAndUntouched.PickRandom();
            else taken = safe.PickRandom();
            presses.Add(taken);
            RotateValue(initGrid, taken, -1);

        } while (pressed.Any(x => !x));
        generationPresses = presses.Reverse<int>().ToList();
    }
    bool AnySelfPoints(ArrowDir[] grid)
    {
        return Enumerable.Range(0, 16).Any(pos =>
        {
            int pointed = GetPointed(pos, grid[pos]);
            return pointed != -1 && GetPointed(pointed, grid[pointed]) == pos; //shortcuts 2 arrows pointing at themselves case.
        });
    }
    void Press(int ix)
    {
        if (moduleSolved || resetting)
            return;
        if (prevPress == ix)
        {
            Log("Tried to press the same arrow twice in a row ({0}). Strike!!!!!!!", coords[ix]);
            Module.HandleStrike();
        }
        else if (IsSafe(ix, dispGrid))
        {
            pressed[ix] = true;
            rings[ix].material = lit;
            RotateValue(dispGrid, ix, 1);
            prevPress = ix;
            Log("Rotated arrow {0} to position {1}.", coords[ix], dispGrid[ix].ToString());
            queues[ix]++;
            if (queues[ix] == 1)
                StartCoroutine(RotateArrow(ix));
            if (pressed.All(x => x))
                Solve();
        }
        else
        {
            Log("Tried to press arrow {0}, which is pointed at by arrow {1}. Strike!!!!!!!", 
                coords[ix], coords[GetAdjacents(ix).First(adj => GetPointed(adj, dispGrid[adj]) == ix)]);
            Module.HandleStrike();
        }
    }
    bool IsSafe(int ix, ArrowDir[] usedGrid)
    {
        return !GetAdjacents(ix).Any(adj => GetPointed(adj, usedGrid[adj]) == ix);
    }
    IEnumerator Reset()
    {
        if (moduleSolved)
            yield break;
        resetting = true;
        yield return new WaitUntil(() => animating.All(x => !x));
        prevPress = -1;
        for (int i = 0; i < 16; i++)
        {
            queues[i] = ((int)initGrid[i] - (int)dispGrid[i] + 4) % 4;
            dispGrid[i] = initGrid[i];
            pressed[i] = false;
            rings[i].material = unlit;
            StartCoroutine(RotateArrow(i));
        }
        yield return new WaitUntil(() => animating.All(x => !x));
        resetting = false;
    }

    void RotateValue(ArrowDir[] grid, int ix, int amount)
    {
        grid[ix] = (ArrowDir) ((((int)grid[ix] + amount) % 4 + 4) % 4);
    }
    int[] GetAdjacents(int pos)
    {
        List<int> output = new List<int>();
        for (int i = 0; i < 4; i++)
            output.Add(GetPointed(pos, (ArrowDir)i));
        return output.Where(x => x != -1).ToArray();
    }
    int GetPointed(int pos, ArrowDir dir)
    {
        switch (dir)
        {
            case ArrowDir.Up:     return pos < 4       ? -1 : pos - 4;
            case ArrowDir.Right:  return pos % 4 == 3  ? -1 : pos + 1;
            case ArrowDir.Down:   return pos >= 12     ? -1 : pos + 4;
            case ArrowDir.Left:   return pos % 4 == 0  ? -1 : pos - 1;
            default: throw new ArgumentOutOfRangeException("No such enum value of " + (int)dir);
        }
    }
    void Solve()
    {
        moduleSolved = true;
        statusRing.material = lit;
        Module.HandlePass();
    }
    void Log(string message, params object[] args)
    {
        Debug.LogFormat("[Lockgrid #{0}] {1}", moduleId, string.Format(message, args));
    }
    Vector3 GetVector(ArrowDir dir)
    {
        return 90 * (int)dir * Vector3.forward;
    }
    IEnumerator RotateArrow(int ix)
    {
        while (queues[ix] > 0)
        {
            animating[ix] = true;
            Transform tf = arrowTFs[ix];
            Vector3 current = tf.localEulerAngles;
            Vector3 target = (current.z + 90) % 360 * Vector3.forward;
            float delta = 0;
            const float duration = 0.5f;
            while (delta < duration)
            {
                yield return null;
                delta += Time.deltaTime;
                tf.localEulerAngles = CWLerp(current, target, delta / duration);
            }
            queues[ix]--;
            animating[ix] = false;
        }
    }

    Vector3 CWLerp(Vector3 a, Vector3 b, float t)
    {
        while (a.z > b.z)
            a -= 360 * Vector3.forward;
        return Vector3.Lerp(a, b, t);
    }
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} foobar> to do something.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string command)
    {
        command = command.Trim().ToUpperInvariant();
        List<string> parameters = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        yield return null;
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
        yield return null;
    }
}
