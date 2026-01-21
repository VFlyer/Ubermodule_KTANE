using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
//using KModkit;
//using System.Text;
using System.Text.RegularExpressions;
using uernd = UnityEngine.Random;

public class UbermoduleHandler : MonoBehaviour {
    public static string[] ignores = null;
    public GameObject button;
    public Renderer screen, component;
    public TextMesh text;
    private List<string> unignoredSolved = new List<string>(); // A list of all modules solved that are NOT in the ignore list.
    private List<string> allSolved = new List<string>(); // A list of all modules solved in general.
    public KMBombModule ModSelf;
    public KMBombInfo Info;
    public KMAudio sound;
    public KMSelectable selectable;
    //public KMModSettings options;
    private static int _moduleIdCounter = 1;
    private int _moduleId = 0;
    private bool isFinal = false;
    private int stagesToGenerate = 0;
    private int[] stagesNum;        // A set of stages to get the xth solved module.
    private string[] InputMethod;   // string determining the input method necessary. "" will be used if neither matches.
    private int currentStage = -1;
    private bool started = false;

    public Material[] materials = new Material[2];

    private int animationLength = 30;
    private List<string> solvables = new List<string>();

    private bool isHolding = false;
    private float timeHeld = 0;
    private List<double> timesHeld = new List<double>();

    public float timerdashthres = 0.5f;
    private bool isplayAnim = false;
    private bool stateduringHold = false;
    private bool countIgnored = true, earlyStageGen, hardModeEnabled = false;
    private bool isCounting = false;

    private readonly string[] startupStrings =
    {
        "Are you sure\nthis works?",
        "Attempt 11\nand counting...",
        "No, This is\nnot Souvenir.",
        "No, I did not\nmake a mistake.",
        "No, This contains\nTap Code.",
        "Just no.",
        "Yes. This\nis a thing.",
        "Oh no!\nNot again!",
        "Bright idea!",
        "...",
        "Simple,\nright?",
        "Just yes.",
        "Yes.",
        "Yes, this\nexists.",
        "Shoutouts.",
        "Walk in\nthe park.",
        "A."
    };

    private readonly string[] startupStringsHardMode = 
    {
        "Oh no!\nNot another\nhard mode!",
        "Hard Mode. Yes.\nIt's here now.",
        "Did you think about this?",
        "Here we go.",
    };

    private string cStageModName = "";

    private bool solved = false;

    public IEnumerator currentlyRunning;

    private UberModuleModSettings settings = new UberModuleModSettings();
    // Use this for initialization
    void Awake()
    {
        _moduleId = _moduleIdCounter++;
        try
        {
            ModConfig<UberModuleModSettings> modConfig = new ModConfig<UberModuleModSettings>("UbermoduleSettings");
            // Read from settings file, or create one if one doesn't exist
            settings = modConfig.Settings;
            // Update settings file incase of error during read
            modConfig.Settings = settings;
            //options.RefreshSettings();

            countIgnored = modConfig.Settings.countIgnoredModules;
            earlyStageGen = modConfig.Settings.generateStagesEarly;
            hardModeEnabled = modConfig.Settings.hardModeEnable;
            timerdashthres = modConfig.Settings.dashTimeLengthModern;
        }
        catch
        {
            Debug.LogErrorFormat("[Übermodule #{0}]: The settings for Übermodule does not work as intended! The module will use default settings instead.", _moduleId);
            timerdashthres = 0.5f;
            countIgnored = true;
            earlyStageGen = true;
            hardModeEnabled = false;
        }
    }
    void Start() {
        //hardModeEnabled = false;
        component.material.color = hardModeEnabled ? Color.red : Color.white;
        currentlyRunning = PlaySolveState();
        selectable.OnInteract += delegate {
            selectable.AddInteractionPunch((float)0.5);
            if (!solved && !isplayAnim) {
                isHolding = true;
                if (currentlyRunning != null)
                    StopCoroutine(currentlyRunning);
                text.color = new Color(text.color.r, text.color.g, text.color.b, (float)1.0);
                if ((currentStage >= 0 && currentStage < stagesNum.Count()) && InputMethod[currentStage].Equals("Morse"))
                {
                    StartCoroutine(ShowMorseInput());
                }
            }
            stateduringHold = isplayAnim;
            return false;
        };
        selectable.OnInteractEnded += delegate {
            if (!solved && !isplayAnim && (!stateduringHold))// Detect if the module is solved, playing an animation, or being held while the animation is playing
            {
                Debug.LogFormat("<Übermodule #{0}> Time held:  {1} {2}.", _moduleId, timeHeld.ToString("0.00"), "second(s)");
                isHolding = false;
                //print (timeHeld);
                timesHeld.Add(timeHeld);
                timeHeld = 0;
                if (isFinal)
                {
                    if (timesHeld.Count() >= 10)
                    {
                        if (stagesNum[currentStage] < 0)
                        {
                            Debug.LogFormat("[Übermodule #{0}] Override detected! The module will now solve itself.", _moduleId);
                            StopCoroutine(currentlyRunning);
                            StartCoroutine(PlaySolveState());
                        }
                        else
                        {
                            Debug.LogFormat("[Übermodule #{0}] Override detected! However stage is valid to input. Strike!", _moduleId);
                            StopCoroutine(currentlyRunning);
                            text.color = new Color(text.color.r, text.color.g, text.color.b, (float)1.0);
                            timesHeld.Clear();
                            ModSelf.HandleStrike();
                            StartCoroutine(PlayStrikeAnim(-1));
                            TapCodeInput1 = 0;
                            Debug.LogFormat("[Übermodule #{0}] Your input has been cleared.", _moduleId);
                        }
                    }
                    else
                    if (InputMethod[currentStage].Equals("Morse"))
                    {
                        currentlyRunning = CheckMorse();
                        StartCoroutine(currentlyRunning);
                    }
                    else
                    if (InputMethod[currentStage].Equals("Tap Code"))
                    {
                        currentlyRunning = CheckTapCode();
                        StartCoroutine(currentlyRunning);
                    }
                }
                else
                {
                    Debug.LogFormat("[Übermodule #{0}] Strike! You cannot interact with the module until the module is in it's \"finale\" phase.", _moduleId);
                    ModSelf.HandleStrike();
                    StartCoroutine(PlayStrikeAnim(-1));
                    timesHeld.Clear();
                }
            }

            return;
        };
        Debug.LogFormat("[Übermodule #{0}] Entering Startup Phase...", _moduleId);
        if (!hardModeEnabled)
            UpdateScreen(startupStrings[uernd.Range(0, startupStrings.Length)]);
        else
            UpdateScreen(startupStringsHardMode[uernd.Range(0, startupStringsHardMode.Length)]);
        if (ignores == null) {
            ignores = GetComponent<KMBossModule>().GetIgnoredModules("Übermodule", new string[] {
                "Bamboozling Time Keeper",
                "Cruel Purgatory",
                "Forget Enigma",
                "Forget Everything",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Organization",
                "Purgatory",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "Übermodule",
                "The Very Annoying Button"
            });
        }
        //Debug.LogFormat("[Übermodule #{0}] Ignored Module List: {1}", _moduleId, FomatterDebugList(ignores));
        // Originally, prints ENTIRE list of Ignored Modules.
        // Übermodule: Don't hang bombs with duplicates of THIS
        // Timing is Everything, Time Keeper, Turn The Key: Bomb Timer sensitive, stalling is NOT FUN.
        // Forget Everything, Forget Enigma, Forget Me Not, Forget Perspective, Forget This, Forget Them All, Forget Us Not: Relies on this module to be solved otherwise without Boss Module Manager detecting this.
        // Tallordered Keys: See above "Forget" Modules
        // Organization: THIS WILL HANG BOMBS IF THIS MODULE'S NAME IS SHOWN.
        // Souvenir: Can eat up a lot of time for some reason from Übermodule?
        // Purgatory + Cruel variant: Rare "last" condtion can hang bombs.
        // The Troll: Worst case senario involves The troll and THIS module involving something along the lines of "The Troll still does not ignore boss modules."
        // There are too many modules to list at this point as of this commit, that I could end up making an entire essay about, which I will not. 
        Info.OnBombExploded += delegate {
            if (solved) return;
            Debug.LogFormat("[Übermodule #{0}] Upon bomb detonation:", _moduleId);
            if (stagesNum == null || !stagesNum.Any())
            {
                Debug.LogFormat("[Übermodule #{0}] Bomb detonated before stages were generated.", _moduleId);
                return;
            }
            if (countIgnored)
                Debug.LogFormat("[Übermodule #{0}] The modules solved to this point are: {1}", _moduleId, FomatterDebugList(allSolved));
            else
                Debug.LogFormat("[Übermodule #{0}] The non-ignored modules solved to this point are: {1}", _moduleId, FomatterDebugList(unignoredSolved));
            for (int x = currentStage + 1; x < stagesNum.Count(); x++)
            {
                if (stagesNum[x] < 0 || stagesNum[x] >= unignoredSolved.Count())
                {
                    Debug.LogFormat("[Übermodule #{0}] Stage {1} would not be accessible.", _moduleId, x + 1);
                }
                else
                {
                    Debug.LogFormat("[Übermodule #{0}] For stage {2}, the number {1} would be visible.", _moduleId, stagesNum[x] + 1, x + 1);
                    Debug.LogFormat("[Übermodule #{0}] The module that was solved for that stage would be {1}.", _moduleId, unignoredSolved[stagesNum[x]]);
                    Debug.LogFormat("[Übermodule #{0}] The defuser would have to input the correct letter in {1}.", _moduleId, InputMethod[x]);
                }
            }
            return;
        };
        ModSelf.OnActivate += delegate {
            UpdateScreen("0");
            started = true;
            // Section used for debugging solvable modules start here.
            solvables = Info.GetSolvableModuleNames().Where(a => !ignores.Contains(a)).ToList();
            if (!solvables.Any())
                Debug.LogFormat("[Übermodule #{0}] There are 0 non-ignored modules.", _moduleId);
            else
            {
                Debug.LogFormat("<Übermodule #{0}> All non-ignored Modules: {1}", _moduleId, solvables.Join(",")); // Prints ENTIRE list of modules not ignored.
                if (solvables.Count() > 20)
                    Debug.LogFormat("[Übermodule #{0}] There are this many non-ignored Modules: {1} Some non-ignored modules are the following: {2}", _moduleId, solvables.Count(), solvables.OrderBy(a => uernd.Range(-32768, 32767)).Take(5).Join(", ")); // Prints the number of non-ignored modules on the bomb and then 5 notable non-ignored modules.
                else
                    Debug.LogFormat("[Übermodule #{0}] Non-ignored Modules: {1}", _moduleId, solvables.Join(",")); // Prints ENTIRE list of modules not ignored.
            }
            List<string> ignored = Info.GetSolvableModuleNames().Where(a => ignores.Contains(a)).ToList();
            Debug.LogFormat("<Übermodule #{0}> Ignored Modules present (including itself): {1}", _moduleId, FomatterDebugList(ignored.ToArray())); // Prints ENTIRE list of modules ignored.
            // Section used for debugging solvable modules end here.

            // Stage Generation begins here.
            if (earlyStageGen) // If the module is able to generate stages early...
            {
                if (hardModeEnabled)
                {
                    Debug.LogFormat("[Übermodule #{0}] Hard Mode is enabled! Normal stage generation procedures have been overridden.", _moduleId);
                    return;
                }
                stagesToGenerate = uernd.Range(3, 5);
                stagesNum = new int[stagesToGenerate];
                InputMethod = new string[stagesToGenerate];

                var numbers = new int[solvables.Count()]; // Bag Randomizer starts here
                for (int p = 0; p < solvables.Count(); p++)
                {
                    numbers[p] = p;
                }
                numbers = numbers.OrderBy(a => uernd.Range(int.MinValue, int.MaxValue)).ToArray();
                // Bag Randomizer ends here
                for (int x = 0; x < stagesToGenerate; x++)
                {
                    var pickState = new string[] { "Tap Code", "Morse" };
                    var RandomState = "";
                    if (x < solvables.Count())
                    {
                        stagesNum[x] = numbers[x];
                        RandomState = pickState[uernd.Range(0, pickState.Count())];
                    }
                    else
                    {
                        stagesNum[x] = -1;
                    }
                    InputMethod[x] = RandomState;
                    if (stagesNum[x] >= 0)
                    {
                        if (RandomState.Equals("Morse"))
                            Debug.LogFormat("[Übermodule #{0}] Generated manditory stage {1} requiring Morse input.", _moduleId, numbers[x] + 1);
                        else if (RandomState.Equals("Tap Code"))
                            Debug.LogFormat("[Übermodule #{0}] Generated manditory stage {1} requiring Tap Code input.", _moduleId, numbers[x] + 1);
                    }
                }
            }
        };
        Debug.LogFormat("[Übermodule #{0}] This module {1} count ignored modules as potential stages.", _moduleId, countIgnored ? "WILL" : "WILL NOT");
        Debug.LogFormat("[Übermodule #{0}] This module will generate stages {1}.", _moduleId, earlyStageGen ? "EARLY" : "LATE");
        Debug.LogFormat("[Übermodule #{0}] All dashes will be registered on the module when holding for more than {1} {2}.", _moduleId, timerdashthres.ToString("0.00"), "second(s)");
    }

    void GenerateLateStages()
    {
        if (hardModeEnabled)
        {
            Debug.LogFormat("[Übermodule #{0}] Hard Mode is enabled! Normal stage generation procedures have been overridden.", _moduleId);
            return;
        }
        stagesToGenerate = uernd.Range(3, 5);
        stagesNum = new int[stagesToGenerate];
        InputMethod = new string[stagesToGenerate];

        var numbers = new int[allSolved.Count()]; // Bag Randomizer starts here
        for (int p = 0; p < allSolved.Count(); p++)
        {
            numbers[p] = p;
        }
        for (int p = 0; p < allSolved.Count(); p++)
        {
            var toreplace = uernd.Range(p, allSolved.Count());
            var temp = numbers[p];
            numbers[p] = numbers[toreplace];
            numbers[toreplace] = temp;
        }// Bag Randomizer ends here
        for (int x = 0; x < stagesToGenerate; x++)
        {
            var pickState = new string[] { "Tap Code", "Morse" };
            var RandomState = "";
            if (x < allSolved.Count())
            {
                stagesNum[x] = numbers[x];
                RandomState = pickState[uernd.Range(0, pickState.Count())];
            }
            else
            {
                stagesNum[x] = -1;
            }
            InputMethod[x] = RandomState;
            if (stagesNum[x] >= 0)
            {
                if (RandomState.Equals("Morse"))
                    Debug.LogFormat("[Übermodule #{0}] Generated manditory stage {1} requiring Morse input.", _moduleId, numbers[x] + 1);
                else if (RandomState.Equals("Tap Code"))
                    Debug.LogFormat("[Übermodule #{0}] Generated manditory stage {1} requiring Tap Code input.", _moduleId, numbers[x] + 1);
            }
        }
    }

    string FomatterDebugList(string[] list) // This one is more used compared to the one underneath.
    {
        string output = "";
        for (int o = 0; o < list.Count(); o++) {
            if (o != 0)
                output += ", ";
            output += list[o];
        }
        return output;
    }
    string FomatterDebugList(List<string> list)
    {
        string output = "";
        for (int o = 0; o < list.Count(); o++) {
            if (o != 0)
                output += ", ";
            output += list[o];
        }
        return output;
    }
    void UpdateScreen(string value) // Update to the given text
    {

        var lowervalue = value.ToLower();
        var largestLength = 0;
        var clength = 0;
        for (int x = 0; x < lowervalue.Length; x++) {
            if (lowervalue.Substring(x, 1).RegexMatch(".")) {
                clength++;
            } else {
                if (clength > largestLength)
                    largestLength = clength;
                clength = 0;
            }
        }
        if (clength > largestLength)
            largestLength = clength;

        text.characterSize = value.Length != 0 ? 0.4f / Mathf.Pow(largestLength, (float)0.9) : 0.4f;

        text.text = value;
    }
    string SplitTextSpecial(string input)
    {
        var checker = input;
        //checker = "Boolean Venn Diagram"; // Used for Testing, 
        var largest = 0;
        var words = checker.Split(new[] { ' ', '|', ',', ' ' }).ToList();
        var splits = new List<int>();
        for (int x = 0; x < words.Count() - 1; x++) {
            if (Math.Abs((words[x + 1].Length + words[x].Length) - largest) <= 2) {
                splits.Add(x + 1);
                x++;
            }
            else if (words[x].Length >= largest || words[x + 1].Length >= largest) {
                splits.Add(x);
            }
        }
        var output = "";
        if (words.Count > 0) {
            for (int x = 0; x < words.Count() - 1; x++) {
                output += words[x];
                if (splits.Contains(x)) {
                    output += "\n";
                } else {
                    output += " ";
                }
            }
            output += words[words.Count() - 1];
        }
        return output;
    }
    // Update is called once per frame
    IEnumerator UpdateSolveCount()
    {
        // Handle Sync Solves by waiting for 3 frames until the end.
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        var list1 = Info.GetSolvedModuleNames().ToList();
        if (countIgnored)// This divides the portion of the code.
        {//This part is for tracking both unignored and ignored modules.
            if (CanUpdateCounterPlusBoss())
            {
                if (CanUpdateCounterNonBoss())
                {
                    var list2 = list1.Where(a => !ignores.Contains(a)).ToList();
                    if (list2.Count() != unignoredSolved.Count())
                    {
                        foreach (string A in unignoredSolved)
                        {
                            list2.Remove(A);
                        }
                        unignoredSolved.AddRange(list2);

                    }
                }
                if (list1.Count() != allSolved.Count())
                {
                    foreach (string A in allSolved)
                    {
                        list1.Remove(A);
                    }
                    if (list1.Count > 1)
                    {
                        list1.Sort();
                        sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.EmergencyAlarm, transform);
                        Debug.LogFormat("[Übermodule #{0}] Multiple modules have been solved within the exact same instance.", _moduleId);
                    }
                    allSolved.AddRange(list1);
                    Debug.LogFormat("[Übermodule #{0}] ---------- {1} Solved ----------", _moduleId, Info.GetSolvedModuleNames().Count());
                    Debug.LogFormat("[Übermodule #{0}] Module(s) Recently Solved: {1}", _moduleId, FomatterDebugList(list1));
                }
            }
            UpdateScreen(allSolved.Count().ToString());
        }
        else
        {//This part is for tracking unignored modules only.
            if (CanUpdateCounterNonBoss())
            {
                var list2 = list1.Where(a => !ignores.Contains(a)).ToList();
                if (list2.Count() != unignoredSolved.Count())
                {
                    foreach (string A in unignoredSolved)
                    {
                        list2.Remove(A);
                    }
                    if (list2.Count > 1)
                    {
                        list2.Sort();
                        sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.EmergencyAlarm, transform);
                        Debug.LogFormat("[Übermodule #{0}] Multiple modules have been solved within the exact same instance.", _moduleId);
                    }

                    unignoredSolved.AddRange(list2);
                    Debug.LogFormat("[Übermodule #{0}] ---------- {1} Solved ----------", _moduleId, Info.GetSolvedModuleNames().Count());
                    Debug.LogFormat("[Übermodule #{0}] Unignored Recently Solved: {1}", _moduleId, FomatterDebugList(list2));

                }
            }
            UpdateScreen(unignoredSolved.Count().ToString());
        }
        if (unignoredSolved.Count() >= solvables.Count())
        {
            if (!earlyStageGen) GenerateLateStages();
            StartCoroutine(PlayFinaleState());
        }
        isCounting = false;
        yield return null;
    }

    void Update()
    {
        if (!solved)
        {
            if (isHolding)
            {
                if (timeHeld <= timerdashthres)
                    timeHeld += Time.deltaTime;
            }
            if (started && !isFinal)
            {
                if (!isCounting)
                {
                    isCounting = true;
                    StartCoroutine(UpdateSolveCount());
                }
            }
        }
    }

    IEnumerator GetStage(int cstage)
    {
        isplayAnim = true;
        sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.MenuButtonPressed, transform);
        if (cstage >= 0 && stagesNum[cstage] >= 0)
        {
            Debug.LogFormat("[Übermodule #{0}] You need to input from the module that was solved {1}{2}.",
                _moduleId, stagesNum[cstage] + 1,
                new[] { 1, 21, 31, 41, 51, 61, 71, 81, 91 }.Contains((stagesNum[cstage] + 1) % 100) ? "st" :
                new[] { 2, 22, 32, 42, 52, 62, 72, 82, 92 }.Contains((stagesNum[cstage] + 1) % 100) ? "nd" :
                new[] { 3, 23, 33, 43, 53, 63, 73, 83, 93 }.Contains((stagesNum[cstage] + 1) % 100) ? "rd" :
                "th"
                );
            if (InputMethod[currentStage].Equals("Morse"))
                Debug.LogFormat("[Übermodule #{0}] You need to input the correct letter in Morse.", _moduleId);
            else if (InputMethod[currentStage].Equals("Tap Code"))
                Debug.LogFormat("[Übermodule #{0}] You need to input the correct letter in Tap Code.", _moduleId);
            UpdateScreen((stagesNum[cstage] + 1).ToString());
            Debug.LogFormat("[Übermodule #{0}] The solved module for that stage was: {1}", _moduleId, countIgnored ? allSolved[stagesNum[cstage]] : unignoredSolved[stagesNum[cstage]]);
        }
        else
        {
            Debug.LogFormat("[Übermodule #{0}] The module has ran out of stages to give.", _moduleId);
            Debug.LogFormat("[Übermodule #{0}] Enforce a solve by clicking on this module 10 times.", _moduleId);
            UpdateScreen("?");
        }
        for (int cnt = 0; cnt < animationLength + 1; cnt++)
        {
            if (InputMethod[currentStage].Equals("Morse")) {
                text.color = new Color(1, 0, 0, (float)cnt / animationLength);
            } else if (InputMethod[currentStage].Equals("Tap Code")) {
                text.color = new Color(0, 0, 1, (float)cnt / animationLength);
            } else {
                text.color = new Color(1, 1, 1, (float)cnt / animationLength);
            }

            yield return new WaitForSeconds(0.005f);
        }
        isplayAnim = false;
    }

    IEnumerator PlayStrikeAnim(int cstage)
    {
        isplayAnim = true;
        bool ccwSpin = uernd.Range(0, 2) == 1;
        for (int cnt = 0; cnt < animationLength; cnt++)
        {
            text.transform.Rotate((ccwSpin ? Vector3.forward : Vector3.back) * 6);
            text.color = new Color(1, 0, 0, (float)(1.0 - (float)cnt / animationLength));
            yield return new WaitForSeconds(0.005f);
        }
        if (isFinal && cstage >= 0 && cstage < stagesNum.Count())
        {
            Debug.LogFormat("[Übermodule #{0}] Revealing module name that was solved that advanced the counter to {1}", _moduleId, stagesNum[cstage] + 1);
            UpdateScreen(SplitTextSpecial(countIgnored ? allSolved[stagesNum[currentStage]] : unignoredSolved[stagesNum[currentStage]]));

        }
        for (int cnt = 0; cnt < animationLength + 1; cnt++)
        {
            if (cnt < animationLength)
                text.transform.Rotate((ccwSpin ? Vector3.forward : Vector3.back) * 6);
            if (!isFinal) {
                text.color = new Color(0, 0, 0, (float)cnt / animationLength);
            }
            else
            {
                if (InputMethod[currentStage].Equals("Morse")) {
                    text.color = new Color(1, 0, 0, (float)cnt / animationLength);
                }
                else if (InputMethod[currentStage].Equals("Tap Code")) {
                    text.color = new Color(0, 0, 1, (float)cnt / animationLength);
                }
            }
            yield return new WaitForSeconds(0.005f);
        }
        isplayAnim = false;
    }
    
    IEnumerator PlayFinaleState()
    {
        isplayAnim = true;
        Debug.LogFormat("[Übermodule #{0}] All non-ignored modules have been solved, activating \"finale\" phase.", _moduleId);
        isFinal = true;
        sound.PlayGameSoundAtTransformWithRef(KMSoundOverride.SoundEffect.LightBuzz, transform);
        for (int cnt = 0; cnt < 4 * animationLength; cnt++) {
            text.color = new Color(text.color.r, text.color.g, text.color.b, (float)(1.0 - (float)cnt / animationLength / 4));

            yield return new WaitForSeconds(0.005f);
            if ((cnt % 30 >= 15 || cnt <= 2 * animationLength) && cnt % 30 < 25)
                screen.material = materials[0];
            else
                screen.material = materials[1];
        }
        screen.material = materials[1];
        if (hardModeEnabled) HandleHardModeStageGen();
        AdvanceStage();

        isplayAnim = false;
    }
    IEnumerator PlaySolveState()
    {
        solved = true;
        ModSelf.HandlePass();
        sound.PlayGameSoundAtTransformWithRef(KMSoundOverride.SoundEffect.CorrectChime, transform);
        Debug.LogFormat("[Übermodule #{0}] Module solved.", _moduleId);
        string[] characters = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
        int randomstartB = uernd.Range(0, characters.Count());
        int randomstartA = uernd.Range(0, characters.Count());
        for (int cnt = 0; cnt < animationLength + 1; cnt++) {
            randomstartA++;
            randomstartB++;
            text.color = new Color(1, 1, 1, (float)cnt / animationLength);
            UpdateScreen(characters[randomstartA % characters.Count()] + characters[randomstartB % characters.Count()]);
            yield return new WaitForSeconds(0.005f);
        }
        while (randomstartA % 26 != 6 || randomstartA < 52) {
            randomstartA++;
            randomstartB++;
            UpdateScreen(characters[randomstartA % characters.Count()] + characters[randomstartB % characters.Count()]);
            yield return new WaitForSeconds(0.005f);
        }
        while (randomstartB % 26 != 6 || randomstartB < 104) {
            randomstartB++;
            UpdateScreen(characters[randomstartA % characters.Count()] + characters[randomstartB % characters.Count()]);
            yield return new WaitForSeconds(0.005f);
        }
        for (float x = 3f; x >= 0; x -= Time.deltaTime)
        {
            text.color = new Color(1, 1, 1, x / 3f);
            yield return null;
        }
        text.color = new Color(1, 1, 1, 0);
    }
    bool CanUpdateCounterNonBoss()
    {
        var list1 = Info.GetSolvedModuleNames().Where(a => !ignores.Contains(a));
        return list1.Count() >= unignoredSolved.Count();
    }
    bool CanUpdateCounterPlusBoss()
    {
        var list1 = Info.GetSolvedModuleNames();
        return list1.Count >= allSolved.Count;
    }
    void AdvanceStage()
    {
        currentStage++;
        if (currentStage >= stagesToGenerate) {
            Debug.LogFormat("[Übermodule #{0}] No more stages to go.", _moduleId);
            StartCoroutine(PlaySolveState());
        } else {
            if (currentStage > 0) {
                Debug.LogFormat("[Übermodule #{0}] Correct character inputted. Moving on to next stage.", _moduleId);
            }
            StartCoroutine(GetStage(currentStage));
        }
    }
    IEnumerator ShowMorseInput()
    {
        while (isHolding) {
            if (timeHeld > timerdashthres) {
                UpdateScreen("\u2013");
            } else {
                UpdateScreen("\u2022");
            }
            yield return null;
        }
        //UpdateScreen ((stagesNum[currentStage]+1).ToString());
        yield return null;
    }
    IEnumerator ReplayMorseInput(string response)
    {
        int index = 0;
        while (index < response.Length)
        {
            string oneinput = response.Substring(index, 1);
            if (oneinput.Equals("-"))
            {
                screen.material = materials[0];
                yield return new WaitForSeconds(0.75f);
                screen.material = materials[1];
                yield return new WaitForSeconds(0.25f);
            }
            else
            if (oneinput.Equals("."))
            {
                screen.material = materials[0];
                yield return new WaitForSeconds(0.25f);
                screen.material = materials[1];
                yield return new WaitForSeconds(0.25f);
            }
            index++;
        }
        yield return null;
    }
    IEnumerator ReplayTapCodeInput(int input1, int input2)
    {
        for (int x = 0; x < input1; x++)
        {
            sound.PlaySoundAtTransform("Tap", transform);
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(0.5f);
        for (int x = 0; x < input2; x++)
        {
            sound.PlaySoundAtTransform("Tap", transform);
            yield return new WaitForSeconds(0.5f);
        }
        yield return null;
    }
    string GetLetterFromMorse(string input)
    {
        switch (input) {
            case ".":
                return "E";
            case "-":
                return "T";
            case ".-":
                return "A";
            case "-.":
                return "N";
            case "--":
                return "M";
            case "..":
                return "I";
            case "...":
                return "S";
            case ".-.":
                return "R";
            case "..-":
                return "U";
            case "-..":
                return "D";
            case ".--":
                return "W";
            case "-.-":
                return "K";
            case "--.":
                return "G";
            case "---":
                return "O";
            case "-.-.":
                return "C";
            case "..-.":
                return "F";
            case "-...":
                return "B";
            case ".--.":
                return "P";
            case "-.--":
                return "Y";
            case "--..":
                return "Z";
            case "...-":
                return "V";
            case "--.-":
                return "Q";
            case ".-..":
                return "L";
            case ".---":
                return "J";
            case "....":
                return "H";
            case "-..-":
                return "X";
            case ".----":
                return "1";
            case "..---":
                return "2";
            case "...--":
                return "3";
            case "....-":
                return "4";
            case ".....":
                return "5";
            case "-....":
                return "6";
            case "--...":
                return "7";
            case "---..":
                return "8";
            case "----.":
                return "9";
            case "-----":
                return "0";
            default:
                return "?";
        }
    }
    string GetFirstValidCharacter(string module)
    {
        var input = module.ToUpper();
        var output = "";
        for (var currentindex = 0; currentindex < input.Length && output.Length == 0; currentindex++)
        {
            var currentLetter = input.Substring(currentindex, 1);
            if (currentLetter.RegexMatch(@"^[A-Z]|[a-z]|[0-9]$"))
            {
                output = currentLetter;
            }
        }
        return output;
    }
    // Hard Mode Handling begins here
    /*
     * The idea of this hard mode variant is to make the defuser have to spell out 1 module name in some given method.
     * Current difference at the moment for submission is 1 stage versus 3-5.
     */
    int currentPos = 0;
    string attemptableStageName;
    IEnumerator PlayStrikeAnimHardMode(string redisplayText)
    {
        isplayAnim = true;
        bool ccwSpin = uernd.Range(0, 2) == 1;
        for (int cnt = 0; cnt < animationLength; cnt++)
        {
            text.transform.Rotate((ccwSpin ? Vector3.forward : Vector3.back) * 6);
            text.color = new Color(1, 0, 0, (float)(1.0 - (float)cnt / animationLength));
            yield return new WaitForSeconds(0.005f);
        }
        
        
        string remainingLetters = redisplayText.Substring(currentPos);
        foreach (char aLetter in remainingLetters)
        {
            UpdateScreen(aLetter.ToString());
            for (int cnt = 0; cnt < 20; cnt++)
            {
                text.transform.Rotate((ccwSpin ? Vector3.forward : Vector3.back) * 18);
                if (!isFinal)
                {
                    text.color = new Color(0, 0, 0, (float)cnt / 20);
                }
                else
                {
                    if (InputMethod[currentStage].Equals("Morse"))
                    {
                        text.color = new Color(1, 0, 0, (float)cnt / 20);
                    }
                    else if (InputMethod[currentStage].Equals("Tap Code"))
                    {
                        text.color = new Color(0, 0, 1, (float)cnt / 20);
                    }
                }
                yield return new WaitForSeconds(0.005f);
            }
            for (int cnt = 20 - 1; cnt >= 0; cnt--)
            {
                text.transform.Rotate((ccwSpin ? Vector3.forward : Vector3.back) * 18);
                if (!isFinal)
                {
                    text.color = new Color(0, 0, 0, (float)cnt / 20);
                }
                else
                {
                    if (InputMethod[currentStage].Equals("Morse"))
                    {
                        text.color = new Color(1, 0, 0, (float)cnt / 20);
                    }
                    else if (InputMethod[currentStage].Equals("Tap Code"))
                    {
                        text.color = new Color(0, 0, 1, (float)cnt / 20);
                    }
                }
                yield return new WaitForSeconds(0.005f);
            }

        }
        if (isFinal)
        {
            Debug.LogFormat("[Übermodule #{0}] Revealing the next letter that should be inputted instead.", _moduleId);
            UpdateScreen(redisplayText.Substring(currentPos, 1));
        }
        for (int cnt = 0; cnt < animationLength + 1; cnt++)
        {
            if (cnt < animationLength)
                text.transform.Rotate((ccwSpin ? Vector3.forward : Vector3.back) * 6);
            if (!isFinal)
            {
                text.color = new Color(0, 0, 0, (float)cnt / animationLength);
            }
            else
            {
                if (InputMethod[currentStage].Equals("Morse"))
                {
                    text.color = new Color(1, 0, 0, (float)cnt / animationLength);
                }
                else if (InputMethod[currentStage].Equals("Tap Code"))
                {
                    text.color = new Color(0, 0, 1, (float)cnt / animationLength);
                }
            }
            yield return new WaitForSeconds(0.005f);
        }
        
        isplayAnim = false;
    }
    string GetAllValidCharacters(string module)
    {
        var input = module.ToUpper();
        var output = "";
        for (var currentindex = 0; currentindex < input.Length; currentindex++)
        {
            var currentLetter = input.Substring(currentindex, 1);
            if (currentLetter.RegexMatch(@"^[A-Z]|[a-z]|[0-9]$"))
            {
                output += currentLetter;
            }
        }
        return output;
    }
    void HandleHardModeStageGen()
    {
        List<string> allSolvableStageDisplayNames = new List<string>();
        List<string> allSolvableStageInputsRequired = new List<string>();
        List<int> allPossibleStages = new List<int>();
        for (int x = 0; x < (earlyStageGen ? unignoredSolved.Count : allSolved.Count); x++)
        {
            var aStageModName = countIgnored ? allSolved[x] : unignoredSolved[x];
            if (aStageModName.RegexMatch(@"^The\s"))
            {
                aStageModName = aStageModName.Substring(4);
            }
            var allLetters = GetAllValidCharacters(aStageModName);
            if (allLetters.Any())
            {
                allSolvableStageInputsRequired.Add(allLetters.ToUpper());
                allSolvableStageDisplayNames.Add(aStageModName);
                allPossibleStages.Add(x);
            }
        }
        stagesToGenerate = 1;
        if (allPossibleStages.Any())
        {
            int idxGivenStage = allPossibleStages.IndexOf(uernd.Range(0, allPossibleStages.Count));
            attemptableStageName = allSolvableStageInputsRequired[idxGivenStage];
            Debug.LogFormat("[Übermodule #{0}] There is at least 1 module on the bomb whose display name contain at least 1 valid letter.", _moduleId);
            Debug.LogFormat("[Übermodule #{0}] Required sequence of letters: {1}", _moduleId, attemptableStageName);
            Debug.LogFormat("[Übermodule #{0}] From the module \"{1}\" (the module that advanced the counter to {2})", _moduleId, allSolvableStageDisplayNames[idxGivenStage], allPossibleStages[idxGivenStage] + 1);
            stagesNum = new int[] { allPossibleStages[idxGivenStage] };
            InputMethod = new string[] { uernd.value < 0.5 ? "Tap Code" : "Morse" };
        }
        else
        {
            stagesNum = new int[] { -1 };
            InputMethod = new string[] { "" };
        }
    }
    
    bool IsCorrectHardModeSubmission(char input)
    {
        if (attemptableStageName != null && currentPos >= 0 && currentPos < attemptableStageName.Length)
            return input == attemptableStageName[currentPos];
        Debug.LogFormat("[Übermodule #{0}] The given attemptable stage name does not have any valid letters. This might be unintended. Auto-assuming the letter given is correct.", _moduleId);
        return true;
    }
    void HandleCorrectHardModeSubmission()
    {
        currentPos++;
        if (currentPos >= attemptableStageName.Length || currentPos < 0)
        {
            Debug.LogFormat("[Übermodule #{0}] Correctly spelt out the remaining letters.", _moduleId);
            StartCoroutine(PlaySolveState());
        }
        else
        {
            sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TitleMenuPressed, transform);
            text.color = new Color(text.color.r, text.color.g, text.color.b, 1f);
            UpdateScreen((1 + stagesNum[0]).ToString()+"+" + currentPos.ToString());
        }
    }
    // End Hard Mode Handling
    bool IsCorrect(string input)
	{
        cStageModName = countIgnored ? allSolved[stagesNum[currentStage]] : unignoredSolved[stagesNum[currentStage]];
        if (Regex.IsMatch(cStageModName, @"^The\s"))// Filter out the word "The " at the start of the module name, if present
        {
            cStageModName = cStageModName.Substring(4);
		}
        var letterRequired = GetFirstValidCharacter(cStageModName);
        if (letterRequired.Any())
        {
            //Debug.LogFormat("[Übermodule #{0}] Checking \"{1}\" with \"{2}\"...", _moduleId, letterRequired, input);
            return input.EqualsIgnoreCase(letterRequired);
        }
        Debug.LogFormat("[Übermodule #{0}] There is no valid detectable character from the given module name. Skipping check...", _moduleId);
        return true;
	}
    IEnumerator CheckMorse()
	{
        for (float cnt = 0; cnt < 1.5f; cnt += Time.deltaTime) // Submission Delay
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, (1.125f - cnt)/1.125f);

            yield return null;
        }
		var morseIn = "";
		for (int x = 0; x < timesHeld.Count (); x++) {
			if (timesHeld [x] > timerdashthres) {
				morseIn += "-";
			} else {
				morseIn += ".";
			}
		}
		var letterInputted = GetLetterFromMorse(morseIn);
		timesHeld.Clear ();
        if (hardModeEnabled)
        {
            if (IsCorrectHardModeSubmission(letterInputted[0]))
            {
                HandleCorrectHardModeSubmission();
            }
            else
            {
                UpdateScreen(letterInputted);
                if (letterInputted.Equals("?"))
                {
                    Debug.LogFormat("[Übermodule #{0}] Strike! The module could NOT reference a valid letter or digit for Morse!", _moduleId);
                    Debug.LogFormat("[Übermodule #{0}] The recorded input: {1} is not valid for Morse.", _moduleId, morseIn);
                }
                else
                {
                    Debug.LogFormat("[Übermodule #{0}] Strike! \"{1}\" was inputted which is not correct!", _moduleId, letterInputted);
                }
                ModSelf.HandleStrike();
                StartCoroutine(PlayStrikeAnimHardMode(attemptableStageName));
                yield return new WaitForSeconds(2f);
                StartCoroutine(ReplayMorseInput(morseIn));
            }
        }
        else
        {
            if (IsCorrect(letterInputted))
            {
                AdvanceStage();
            }
            else
            {
                UpdateScreen(letterInputted);
                if (letterInputted.Equals("?"))
                {
                    Debug.LogFormat("[Übermodule #{0}] Strike! The module could NOT reference a valid letter or digit for Morse!", _moduleId);
                    Debug.LogFormat("[Übermodule #{0}] The recorded input: {1} is not valid for Morse.", _moduleId, morseIn);
                }
                else
                {
                    Debug.LogFormat("[Übermodule #{0}] Strike! \"{1}\" was inputted which is not correct!", _moduleId, letterInputted);
                }
                ModSelf.HandleStrike();
                StartCoroutine(PlayStrikeAnim(currentStage));
                yield return new WaitForSeconds(2f);
                StartCoroutine(ReplayMorseInput(morseIn));
            }
        }
		yield return null;
	}
	private int TapCodeInput1 = 0;
	IEnumerator CheckTapCode()
	{
		var GridLetters = new[,] {
			 {"A","B","C","D","E","1"},
			 {"F","G","H","I","J","2"},
			 {"L","M","N","O","P","3"},
			 {"Q","R","S","T","U","4"},
			 {"V","W","X","Y","Z","5"},
			 {"6","7","8","9","0","K"}
		};// Grid for Tap Code, not a lot of use otherwise.
        for (float cnt = 0; cnt < 1.5f; cnt += Time.deltaTime) // Submission Delay
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, (1.125f - cnt) / 1.125f);

            yield return null;
        }
		sound.PlaySoundAtTransform ("MiniTap",transform);
		if (TapCodeInput1 == 0) {
			TapCodeInput1 = timesHeld.Count ();
			timesHeld.Clear ();
            text.color = new Color(text.color.r, text.color.g, text.color.b, 1f);
		}
        else
		{
            var TapCodeInput2 = timesHeld.Count();
			timesHeld.Clear ();
			var letterInputted = "?";
			if ((TapCodeInput1 >= 1 && TapCodeInput1 <= 6) && (TapCodeInput2 >= 1 && TapCodeInput2 <= 6)) {
				letterInputted = GridLetters [TapCodeInput1 - 1, TapCodeInput2 - 1];
			}
            if (hardModeEnabled)
            {
                if (IsCorrectHardModeSubmission(letterInputted[0]))
                {
                    HandleCorrectHardModeSubmission();
                }
                else
                {
                    UpdateScreen(letterInputted);
                    if (letterInputted.Equals("?"))
                    {
                        Debug.LogFormat("[Übermodule #{0}] Strike! The module could NOT reference a valid letter or digit for Tap Code!", _moduleId);
                        Debug.LogFormat("[Übermodule #{0}] The recorded input: {1}, {2} is not a valid for Tap Code.", _moduleId, TapCodeInput1, TapCodeInput2);
                    }
                    else
                    {
                        Debug.LogFormat("[Übermodule #{0}] Strike! \"{1}\" was inputted which is not correct!", _moduleId, letterInputted);
                    }
                    ModSelf.HandleStrike();
                    StartCoroutine(PlayStrikeAnimHardMode(attemptableStageName));
                    yield return new WaitForSeconds(2f);
                    StartCoroutine(ReplayTapCodeInput(TapCodeInput1, TapCodeInput2));
                }
            }
            else
            if (IsCorrect(letterInputted)) {
				AdvanceStage ();
			} else {
				UpdateScreen (letterInputted);
				if (letterInputted.Equals ("?")) {
					Debug.LogFormat("[Übermodule #{0}] Strike! The module could NOT reference a valid letter or digit for Tap Code!",_moduleId);
					Debug.LogFormat("[Übermodule #{0}] The recorded input: {1}, {2} is not a valid for Tap Code.",_moduleId,TapCodeInput1,TapCodeInput2);
				} else {
					Debug.LogFormat("[Übermodule #{0}] Strike! \"{1}\" was inputted which is not correct!",_moduleId,letterInputted);
				}
				ModSelf.HandleStrike ();
				StartCoroutine (PlayStrikeAnim (currentStage));
                yield return new WaitForSeconds(2f);
                StartCoroutine(ReplayTapCodeInput(TapCodeInput1, TapCodeInput2));
            }
			TapCodeInput1 = 0;
		}
		yield return null;
	}
    //KM Mod Settings/Settings for Ubermodule
    public class UberModuleModSettings
    {
        public bool countIgnoredModules = true;
        public bool generateStagesEarly = true;
        public bool hardModeEnable = false;
        public float dashTimeLengthModern = 0.5f;
    }
    static readonly Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
      {
            new Dictionary<string, object>
            {
                { "Filename", "ubermodulesettings.json" },
                { "Name", "Ubermodule Settings" },
                { "Listings", new List<Dictionary<string, object>>{
                    new Dictionary<string, object>
                    {
                        { "Key", "countUnignoredModules" },
                        { "Text", "Count All Modules" },
                        { "Description", "Includes ignored modules as possible stages." },
                    },
                    new Dictionary<string, object>
                    {
                        { "Key", "generateStagesEarly" },
                        { "Text", "Early Stage Generation" },
                        { "Description", "Generates stages for Ubermodule as early as possible. This only considers solvable modules on the bomb, rather than all stages." }
                    },
                    new Dictionary<string, object>
                    {
                        { "Key", "hardModeEnable" },
                        { "Text", "Enable Hard Mode" },
                        { "Description", "Make Übermodule start in hard mode. This requires inputting the ENTIRE sequence of letters to disarm the module for only 1 stage." }
                    },
                    new Dictionary<string, object>
                    {
                        { "Key", "dashTimeLengthModern" },
                        { "Text", "Dash Time Threshold" },
                        { "Description", "The time it takes to hold for the module to interept a dash instead of a dot." }
                    },
                } }
            }
      };
    //Twitch Plays Handler
	#pragma warning disable 414
    private readonly string TwitchHelpMessage = "To submit with Tap code, use !{0} tap/press 42 (Must be exactly two numbers and must be in the range of 1 to 9). To submit with Morse code, use !{0} transmit/tx -..- To click the screen multiple times until the module solves, use !{0} spam. If there is already 1 input in Tap code, use !{0} tap 5 to enter the second input (must exactly be a single number) or !{0} reset to reset the input. However, reset command will NOT work if Morse Code was entered when the module was expecting Tap Code";
    #pragma warning restore 414
	
	private bool morseCodeOnTapCode = false;
    IEnumerator ProcessTwitchCommand(string command)
    {
        // Old TP Handler by kavinkul
        
		int inputMode = 0;
		bool isSecond = false;
		command = command.ToLowerInvariant();
		command = command.TrimStart();
		command = command.TrimEnd();
		
		if(TapCodeInput1 == 0) morseCodeOnTapCode = false; //Ensure that the reset command would work the second time after morse code type input is entered into tap code.
		if(command.Equals("reset"))
		{
            yield return null;
			if(!morseCodeOnTapCode) TapCodeInput1 = 0; //Reset Tap code input in a case where the command is unexpectedly halted, or there is a direct interaction to the module outside TP.
			else yield return "sendtochaterror Can't reset the module due to Morse Code was used to enter the first input of Tap Code.";
            yield break;
		}
		else if(command.Equals("spam"))
		{
			inputMode = 3;
		}
		else if(command.StartsWith("tap ")) 
		{
			command = command.Substring(4);
			inputMode = 1;
		}
        else if(command.StartsWith("press ")) 
		{
			command = command.Substring(6);
			inputMode = 1;
		}
        else if(command.StartsWith("tx ")) 
		{
			command = command.Substring(3);
			inputMode = 2;
		}
		else if(command.StartsWith("transmit ")) 
		{
			command = command.Substring(9);
			inputMode = 2;
		}
		else 
		{
            yield return "sendtochaterror Valid commands are tap, press, tx, transmit, spam, and reset.";
            yield break;
        }
		if (inputMode >= 1 && inputMode <= 3 && !isFinal) inputMode = 4;
		command = command.Trim();
		int[] input = new int[command.Length];
		
        ///* Parsing and validate the input string. */
        
		int outnumber;
		switch(inputMode)
		{
			case 1: // Tap Code Processing
			// If no input has been entered, then expects two taps. If a single input have been entered, then expects one tap. 
			if ((TapCodeInput1 == 0 && command.Length == 2)||(TapCodeInput1 != 0 && command.Length == 1))
			{
				for (int i = 0; i < command.Length; i++)
				{
					if(int.TryParse(command, out outnumber) && command[i] != '0') input[i] = command[i] - '0';
					else 
					{
						yield return "sendtochaterror Invalid commands: Tap code valid characters are numbers from 1 to 9.";
						yield break;
					}
				}
			}
			else
			{
                if (command.RegexMatch(@"^\d\s\d$"))
                    {
                        var possibleDigits = command.Split();
                        var pointerPos = 0;
                        foreach (string aDigit in possibleDigits)
                        {
                            if (!int.TryParse(aDigit, out outnumber) || outnumber <= 0 || outnumber > 9)
                            {
                                yield return "sendtochaterror Invalid commands: Tap code valid characters are numbers from 1 to 9.";
                                yield break;
                            }
                            input[pointerPos] = outnumber;
                            pointerPos++;
                        }
                        input = input.Take(2).ToArray();
                        break;
                    }

                if(TapCodeInput1 == 0) yield return "sendtochaterror Invalid commands: Expect a PAIR of numbers for Tap code.";
				else yield return "sendtochaterror Invalid commands: Expect a SINGLE number for Tap code.";
				yield break;
			}				
			break;
			case 2: // Morse Code Processing
			for (int i = 0; i < command.Length; i++)
			{
				switch(command[i])
				{
					case '.':
					input[i] = 0;
					break;
					case '-':
					input[i] = 1;
					break;
					default :
					yield return string.Format("sendtochaterror Invalid morse character \"{0}\": Acceptable inputs for Morse code are '.', '-'.",command[i]);
					yield break;
				}
			}
			break;
		}
		
		yield return null;
		
		switch(inputMode){
			case 1:
			foreach(int k in input)
			{
				for(int i = 0; i < k; i++)
				{
					yield return selectable;
					yield return new WaitForSeconds(0.05f);
					yield return selectable;
					yield return new WaitForSeconds(0.05f);
				}
				// Check if the expected inputs must be in Tap Code, whether it is the first input set, and ensure that there is no more than 9 taps.
				if (!isSecond && InputMethod[currentStage].Equals("Tap Code") && k <= 9)
				{
					yield return new WaitUntil(() => TapCodeInput1 != 0);
					isSecond = true;
					yield return "trycancel The Tap code submission has been canceled. The first input is " + TapCodeInput1.ToString() + ".";
				}
			}
			yield return "solve";
			yield return "strike";
			break;
			case 2:
			foreach(int k in input)
			{
				int j = k;
				if (InputMethod[currentStage].Equals("Tap Code"))
				{
					j = 0; //Convert all dashes to a single tap since the selectable cannot be hold in Tap Code mode.
					morseCodeOnTapCode = true; //Prevent player from resetting the input when wrong type of answer is entered.
				}
				switch(j)
				{
					case 0:
					yield return selectable;
					yield return new WaitForSeconds(0.05f);
					yield return selectable;
					yield return new WaitForSeconds(0.05f);
					break;
					case 1:
					yield return selectable;
					yield return new WaitUntil(() => timeHeld > timerdashthres);
                    //Debug.LogFormat("<Übermodule #{0}> {1} > {2}", _moduleId,timeHeld,timerdashthres);
                    yield return selectable;
					yield return new WaitForSeconds(0.1f);					
					break;
				}
			}
			yield return "solve";
			yield return "strike";
			break;
			case 3:
			for (int i = 0; i < 10; i++)
			{
				yield return selectable;
				yield return new WaitForSeconds(0.05f);
				yield return selectable;
				yield return new WaitForSeconds(0.05f);
			}
			yield return "solve";
			yield return "strike";
			break;
			case 4:
			// When the module is not ready to solve. 
			yield return selectable;
			yield return new WaitForSeconds(0.05f);
			yield return selectable;
			yield return new WaitForSeconds(0.05f);
			yield return "strike";
			break;
		}
        yield break;
	}

    private readonly string[] forceSolveTexts = new string[] { "It was\nauto-solved!\n:'(", "Halting...", "Auto-solved.", "Auto-solved.\n:(" };
	IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        solved = true;
        UpdateScreen(forceSolveTexts[uernd.Range(0,forceSolveTexts.Count())]);
        StopAllCoroutines();
        enabled = false;
        Debug.LogFormat("[Übermodule #{0}] Module forced-solved viva TP solve command.", _moduleId);
    }
}
