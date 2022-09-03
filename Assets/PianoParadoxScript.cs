using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

public class PianoParadoxScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMBossModule BossModule;

    public GameObject[] KeyObjs;
    private KMSelectable[] KeySels = new KMSelectable[12];
    public AudioClip[] PianoSounds;
    public FakeStatusLight FakeStatusLight;
    public Material[] KeyMats;
    public TextMesh ScreenText;
    private static readonly int[] _keyColors = new int[12] { 0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0 };

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private static readonly string[] _keyNames = new string[12] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private string[] _ignoredModules;
    private int _stageCount;
    private int _currentSolves;
    private int _currentStage = -1;
    private Coroutine _showStage;

    private bool _submissionPhase;
    private bool _readyToAdvance;

    private readonly List<int> _displayedNotes = new List<int>();
    private readonly List<int> _requiredInputs = new List<int>();
    private readonly List<int> _offsetInputs = new List<int>();
    private readonly int[] _offsets = new int[6];
    private static readonly int[] _goalNotes = new int[6] { 10, 9, 5, 7, 9, 10 };
    private int _inputIx;
    private bool _realModuleSolved;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < KeyObjs.Length; i++)
        {
            KeySels[i] = KeyObjs[i].GetComponent<KMSelectable>();
            KeySels[i].OnInteract += KeyPress(i);
        }
        FakeStatusLight = Instantiate(FakeStatusLight);
        FakeStatusLight.GetStatusLights(transform);
        FakeStatusLight.Module = Module;
        Module.OnActivate += Activate;
        StartCoroutine(Init());
    }

    private void Activate()
    {
        _readyToAdvance = true;
    }

    private IEnumerator Init()
    {
        yield return null;
        if (_ignoredModules == null)
            _ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Piano Paradox", new string[] { "Piano Paradox" });
        _stageCount = BombInfo.GetSolvableModuleNames().Count(i => !_ignoredModules.Contains(i));
        if (_stageCount == 0)
        {
            StartCoroutine(SolveAnimation());
            yield break;
        }
        for (int i = 0; i < _stageCount; i++)
            _displayedNotes.Add(Rnd.Range(0, 12));
        for (int i = _stageCount - 1; i > _stageCount - 7 && i >= 0; i--)
            _offsets[5 - (_stageCount - 1 - i)] = (_goalNotes[5 - (_stageCount - 1 - i)] - _displayedNotes[i] + 12) % 12;
        for (int i = _stageCount - 1; i >= 0; i--)
        {
            int offset = _offsets[((5 - (_stageCount - i - 1)) % 6) < 0 ? ((5 - (_stageCount - i - 1)) % 6) + 6 : ((5 - (_stageCount - i - 1)) % 6)];
            _offsetInputs.Insert(0, offset);
            _requiredInputs.Insert(0, (_displayedNotes[i] + offset + 12) % 12);
        }
        for (int i = 0; i < _stageCount; i++)
        {
            Debug.LogFormat("[Piano Paradox #{0}] ============ STAGE {1} ============", _moduleId, i + 1);
            Debug.LogFormat("[Piano Paradox #{0}] Displayed note: {1}.", _moduleId, _keyNames[_displayedNotes[i]]);
            Debug.LogFormat("[Piano Paradox #{0}] The required offset is +{1} semitones.", _moduleId, _offsetInputs[i]);
            Debug.LogFormat("[Piano Paradox #{0}] The input for this stage is {1}.", _moduleId, _keyNames[_requiredInputs[i]]);
        }
        Debug.LogFormat("[Piano Paradox #{0}] ===================================", _moduleId);
        Debug.LogFormat("[Piano Paradox #{0}] Full input: {1}", _moduleId, _requiredInputs.Select(i => _keyNames[i]).Join(" "));
    }

    private void Update()
    {
        if (!_readyToAdvance)
            return;
        _currentSolves = BombInfo.GetSolvedModuleNames().Count(i => !_ignoredModules.Contains(i));
        if (_currentStage == _currentSolves)
            return;
        if (_currentSolves <= _stageCount)
            Advance();
    }

    private void Advance()
    {
        _currentStage++;
        if (_currentStage != _stageCount)
        {
            ScreenText.text = (_currentStage + 1).ToString();
            for (int i = 0; i < 12; i++)
            {
                if (_displayedNotes[_currentStage] == i)
                    KeyObjs[i].GetComponent<MeshRenderer>().material = KeyMats[2];
                else
                    KeyObjs[i].GetComponent<MeshRenderer>().material = KeyMats[_keyColors[i]];
            }
        }
        else
        {
            ScreenText.text = (_inputIx + 1).ToString();
            for (int i = 0; i < 12; i++)
                KeyObjs[i].GetComponent<MeshRenderer>().material = KeyMats[_keyColors[i]];
        }
        if (_showStage != null)
            StopCoroutine(_showStage);
        _showStage = StartCoroutine(ShowStage());
    }

    private IEnumerator ShowStage()
    {
        _readyToAdvance = false;
        if (_currentStage == _stageCount)
            _submissionPhase = true;
        if (_currentStage != _stageCount)
        {
            yield return new WaitForSeconds(1f);
            _readyToAdvance = true;
        }
        yield break;
    }

    private Coroutine _strikeAnimation;

    private KMSelectable.OnInteractHandler KeyPress(int i)
    {
        return delegate ()
        {
            Audio.PlaySoundAtTransform(PianoSounds[i].name, transform);
            if (_moduleSolved || !_submissionPhase)
                return false;
            if (i == _requiredInputs[_inputIx])
            {
                Debug.LogFormat("[Piano Paradox #{0}] Correctly pressed {1}.", _moduleId, _keyNames[i]);
                for (int j = 0; j < 12; j++)
                    KeyObjs[j].GetComponent<MeshRenderer>().material = KeyMats[_keyColors[j]];
                _inputIx++;
                ScreenText.text = (_inputIx + 1).ToString();
                if (_inputIx == _stageCount)
                {
                    StartCoroutine(SolveAnimation());
                    ScreenText.text = "-----";
                }
            }
            else
            {
                Debug.LogFormat("[Piano Paradox #{0}] Pressed {1}, when {2} was expected. Strike.", _moduleId, _keyNames[i], _keyNames[_requiredInputs[_inputIx]]);
                int st = _inputIx;
                while ((st + 6) < _stageCount)
                    st += 6;
                Debug.Log(st);
                ScreenText.text = (_inputIx + 1).ToString() + "   " + _keyNames[_displayedNotes[st]].ToString();
                Module.HandleStrike();
                for (int j = 0; j < 12; j++)
                {
                    if (_displayedNotes[_inputIx] == j)
                        KeyObjs[j].GetComponent<MeshRenderer>().material = KeyMats[2];
                    else
                        KeyObjs[j].GetComponent<MeshRenderer>().material = KeyMats[_keyColors[j]];
                }
                if (_strikeAnimation != null)
                    StopCoroutine(_strikeAnimation);
                _strikeAnimation = StartCoroutine(StrikeAnimation());
            }
            return false;
        };
    }

    private IEnumerator StrikeAnimation()
    {
        FakeStatusLight.SetStrike();
        yield return new WaitForSeconds(1f);
        FakeStatusLight.SetInActive();
    }

    private IEnumerator SolveAnimation()
    {
        if (_strikeAnimation != null)
            StopCoroutine(_strikeAnimation);
        _moduleSolved = true;
        FakeStatusLight.SetPass();
        yield return new WaitForSeconds(0.5f);
        FakeStatusLight.SetInActive();
        Audio.PlaySoundAtTransform("SnapSnap", transform);
        yield return new WaitForSeconds(0.5f);
        FakeStatusLight.SetPass();
        Module.HandlePass();
        _realModuleSolved = true;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} play A# A F G A A# [Play notes A#, A, F, G, A, A#.]";
#pragma warning restore 0414
    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToUpperInvariant();
        var parameters = command.Split(' ');
        var m = Regex.Match(parameters[0], @"^\s*play\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        var list = new List<int>();
        for (int i = 1; i < parameters.Length; i++)
        {
            if (!_keyNames.Contains(parameters[i]))
                yield break;
            list.Add(Array.IndexOf(_keyNames, parameters[i]));
        }
        if (!_submissionPhase)
        {
            yield return "sendtochaterror It's not time to submit yet! Command ignored.";
            yield break;
        }
        yield return null;
        yield return "strike";
        yield return "solve";
        var waitTime = 0.2f;
        for (int i = 0; i < list.Count; i++)
        {
            KeySels[list[i]].OnInteract();
            if (list.Count >= 6 && list[list.Count - 6] == 10 && list[list.Count - 5] == 9 && list[list.Count - 4] == 5 && list[list.Count - 3] == 7 && list[list.Count - 2] == 9 && list[list.Count - 1] == 10)
            {
                if ((i == list.Count - 6) || (i == list.Count - 4) || (i == list.Count - 2))
                    waitTime = 0.16f;
                if ((i == list.Count - 5) || (i == list.Count - 3) || (i == list.Count - 1))
                    waitTime = 0.32f;
                if (list.Count > 6 && i == list.Count - 7)
                    waitTime = 0.6f;
            }
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!_submissionPhase)
            yield return true;
        var list = new List<int>();
        var waitTime = 0.2f;
        for (int i = 0; i < _requiredInputs.Count; i++)
            list.Add(_requiredInputs[i]);
        for (int i = 0; i < list.Count; i++)
        {
            KeySels[list[i]].OnInteract();
            if (list.Count >= 6 && list[list.Count - 6] == 10 && list[list.Count - 5] == 9 && list[list.Count - 4] == 5 && list[list.Count - 3] == 7 && list[list.Count - 2] == 9 && list[list.Count - 1] == 10)
            {
                if ((i == list.Count - 6) || (i == list.Count - 4) || (i == list.Count - 2))
                    waitTime = 0.16f;
                if ((i == list.Count - 5) || (i == list.Count - 3) || (i == list.Count - 1))
                    waitTime = 0.32f;
                if (list.Count > 6 && i == list.Count - 7)
                    waitTime = 0.6f;
            }
            yield return new WaitForSeconds(waitTime);
        }
        while (!_realModuleSolved)
            yield return true;
    }
}
