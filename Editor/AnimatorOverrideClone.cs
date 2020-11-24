using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class AnimatorOverrideClone : EditorWindow
{

    private AnimatorController sourceAnim;
    private Texture2D sourceSheet;
    private int numDestSheets = 1;
    private Texture2D[] destSheets = { };
    private string oldNameText;
    private string[] newNameText = { };

    
    // Creates a new option in "Windows"
    [MenuItem("Window/Clone animator for spritesheet override")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        AnimatorOverrideClone window = (AnimatorOverrideClone)EditorWindow.GetWindow(typeof(AnimatorOverrideClone), false, "Animator Clone");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Space(25f);
                
        GUILayout.BeginHorizontal();
        GUILayout.Label("Original Animation Controller:", EditorStyles.boldLabel);
        sourceAnim = (AnimatorController)EditorGUILayout.ObjectField(sourceAnim, typeof(AnimatorController), false, GUILayout.Width(220));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Original Spritesheet:", EditorStyles.boldLabel);
        sourceSheet = (Texture2D)EditorGUILayout.ObjectField(sourceSheet, typeof(Texture2D), false, GUILayout.Width(220));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Text to replace in animator name:", EditorStyles.boldLabel);
        oldNameText = EditorGUILayout.TextField(oldNameText, GUILayout.Width(220));
        GUILayout.EndHorizontal();

        GUILayout.Space(25f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Number of spitesheets to apply:", EditorStyles.boldLabel);
        numDestSheets = EditorGUILayout.IntSlider(numDestSheets, 1, 10, GUILayout.Width(220));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Spritesheets to clone to:", EditorStyles.boldLabel);
        GUILayout.EndHorizontal();
        // resize the destination sheet array if the number requested has changed
        if (destSheets.Length != numDestSheets)
        {
            Texture2D[] temp = new Texture2D[numDestSheets];
            string[] prefix_temp = new string[numDestSheets];
            int entriesToCopy = numDestSheets > destSheets.Length ? destSheets.Length : numDestSheets;
            System.Array.Copy(destSheets, temp, entriesToCopy);
            System.Array.Copy(newNameText, prefix_temp, entriesToCopy);
            destSheets = temp;
            newNameText = prefix_temp;
        }
        

        for (int i = 0; i < numDestSheets; i++)
        {
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Destination Spritesheet:", EditorStyles.boldLabel);
            destSheets[i] = (Texture2D)EditorGUILayout.ObjectField(destSheets[i], typeof(Texture2D), false, GUILayout.Width(220));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Replacement text for new animations:", EditorStyles.boldLabel);
            newNameText[i] = EditorGUILayout.TextField(newNameText[i], GUILayout.Width(220));
            GUILayout.EndHorizontal();
        }


        GUILayout.Space(25f);
        if (GUILayout.Button("Create animator override using new spritesheets"))
        {
            CloneAnimatorController(sourceAnim);
        }
    }


    private void CloneAnimatorController(AnimatorController source)
    {
        for (int i = 0; i < destSheets.Length; i++)
        {
            CopySpritesheetSlices(sourceSheet, destSheets[i]);
            var clipList = CopyAnimationsToNewSheet(source.animationClips, sourceSheet, destSheets[i], oldNameText, newNameText[i]);
            CreateAndPopulateOverrideController(sourceAnim, clipList, oldNameText, newNameText[i]);
        }        
    }


    // Copies a set of animations to use a new spritesheet.  Returns the set of new clips, in the same order as the original.
    // Assumes that the destination spritesheet has already been sliced to match the source (to avoid reslicing repeatedly)
    private List<KeyValuePair<AnimationClip, AnimationClip>> 
        CopyAnimationsToNewSheet(AnimationClip[] sourceAnims, Texture2D sourceSheet, Texture2D destSheet, 
        string oldPrefix, string newPrefix)
    {
        if (sourceAnims == null) return null;

        var newClips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        for (int i = 0; i < sourceAnims.Length; i++)
        {
            AnimationClip sourceAnim = sourceAnims[i];

            string sourceGuid = ReadSpritesIDsFromSheetMeta(sourceSheet, out string[] sourceSpriteIDs);
            int[] animSpriteNums = GetSpriteNumbersFromAnimation(sourceSheet, sourceAnim);
            string destGuid = ReadSpritesIDsFromSheetMeta(destSheet, out string[] destSpriteIDs);
                        
            string sourcePath = AssetDatabase.GetAssetPath(sourceAnim);

            // defaults, for if prefix boxes are left blank
            if (newPrefix == null || newPrefix == "")
            {
                newPrefix = destSheet.name;
            }
            if (oldPrefix == null || oldPrefix == "")
            {
                oldPrefix = sourceAnim.name;
            }

            string destPath = (new Regex(oldPrefix)).Replace(sourcePath, newPrefix);
            if (destPath == sourcePath) destPath = "Assets/blank_copy.anim";

            bool worked = AssetDatabase.CopyAsset(sourcePath, destPath);
            string animFile = File.ReadAllText(destPath);
            string newAnimFile = animFile;
            for (int j = 0; j < animSpriteNums.Length; j++)
            {
                string replaceGuid = "value: {fileID: " + sourceSpriteIDs[animSpriteNums[j]] + ", guid: " + sourceGuid + ",";
                string newGuidString = "value: {fileID: " + destSpriteIDs[animSpriteNums[j]] + ", guid: " + destGuid + ",";
                var regexReplace = new Regex(replaceGuid);
                newAnimFile = regexReplace.Replace(newAnimFile, newGuidString);
            }

            File.WriteAllText(destPath, newAnimFile);
            AssetDatabase.Refresh();

            var newClipPair = new KeyValuePair<AnimationClip, AnimationClip>(sourceAnim, (AnimationClip)AssetDatabase.LoadAssetAtPath(destPath, typeof(AnimationClip)));
            newClips.Add(newClipPair);
        }

        return newClips;
    }


    private void CreateAndPopulateOverrideController(AnimatorController sourceAnimator, List<KeyValuePair<AnimationClip, AnimationClip>> overrideClips, string oldPrefix, string newPrefix)
    {
        AnimatorOverrideController newAnimOverride = new AnimatorOverrideController();
        newAnimOverride.runtimeAnimatorController = sourceAnimator;

        var overrideList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        newAnimOverride.ApplyOverrides(overrideClips);

        string sourcePath = AssetDatabase.GetAssetPath(sourceAnimator);
        //Debug.Log("Source path: " + sourcePath);
        string destPath = (new Regex(oldPrefix)).Replace(sourcePath, newPrefix);
        //Debug.Log("Destination path: " + destPath);
        destPath = Path.ChangeExtension(destPath, "overrideController");
        //Debug.Log("New destination path: " + destPath);
        AssetDatabase.CreateAsset(newAnimOverride, destPath);
    }


    // returns spritesheet's guid and fills spriteIDs with fileID for each sprite in the sheet metadata
    private string ReadSpritesIDsFromSheetMeta(Texture2D spritesheet, out string[] spriteIDs)
    {
        string assetPath = AssetDatabase.GetAssetPath(spritesheet);
        string metaFile = File.ReadAllText(assetPath + ".meta");

        string guidPattern = "guid: ([\\w]*)\r?\n";
        string spriteIDpattern = "internalID: (-?[\\d]*)\r?\n";
        var regexGuid = new Regex(guidPattern);
        var regexSprites = new Regex(spriteIDpattern);
        string guid = regexGuid.Match(metaFile).Groups[1].ToString();
        var matches = regexSprites.Matches(metaFile);
        spriteIDs = new string[matches.Count];

        int i = 0;
        foreach (Match m in matches)
        {
            // record the ID string and then increment counter i
            spriteIDs[i++] = m.Groups[1].ToString();
        }
        return guid;
    }


    // populates an array of the sprites used in an AnimationClip
    private int[] GetSpriteNumbersFromAnimation(Texture2D spriteSheet, AnimationClip anim)
    {
        string spriteSheetGuid = ReadSpritesIDsFromSheetMeta(spriteSheet, out string[] spriteIDs);
        var spriteDict = CreateSpriteLookupDict(spriteIDs);
        List<int> spriteNums = new List<int>();

        string animPath = AssetDatabase.GetAssetPath(anim);
        string metaFile = File.ReadAllText(animPath);

        string animIdPattern = "value: {fileID: (-?[\\d]*), guid: (\\w*),";
        var regexAnim = new Regex(animIdPattern);
        var matches = regexAnim.Matches(metaFile);
        foreach (Match m in matches)
        {
            GroupCollection groups = m.Groups;
            spriteNums.Add(spriteDict[groups[1].ToString()]);
        }

        return spriteNums.ToArray();
    }


    // takes an array of spriteIDs and creates an inverse lookup dictionary to get sprite number from ID string
    private Dictionary<string, int> CreateSpriteLookupDict(string[] spriteIDs)
    {
        var outDict = new Dictionary<string, int>();
        for (int i = 0; i < spriteIDs.Length; i++)
        {
            outDict.Add(spriteIDs[i], i);
        }
        return outDict;
    }




    // Transfers spriteslicing from source to destination spritesheet.  Modified from Rampe's code on Unity forum: https://forum.unity.com/threads/copy-spritesheet-slices-and-pivots-solved.301340/
    private void CopySpritesheetSlices(Texture2D source, Texture2D dest)
    {
        if (source.GetType() != typeof(Texture2D) || dest.GetType() != typeof(Texture2D))
        {
            Debug.LogError("Both source and destination spritesheets must be Texture2D objects.");
            return;
        }

        if (source.height != dest.height || source.width != dest.width)
        {
            Debug.LogError("Dimensions of spritesheets must match.  Source: " + source.height + "x" + source.width + "; Destination: " + dest.height + "x" + dest.width);
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(source);
        TextureImporter ti1 = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        ti1.isReadable = true;

        string destPath = AssetDatabase.GetAssetPath(dest);
        TextureImporter ti2 = AssetImporter.GetAtPath(destPath) as TextureImporter;
        ti2.isReadable = true;

        // copy settings over, then toggle import mode off/on to reset any existing sprites
        EditorUtility.CopySerialized(ti1, ti2);        
        // the below can be used to rename the sprites, if desired.  TODO: figure this out, maybe give options for renaming or not.
        ti2.spriteImportMode = SpriteImportMode.Single;
        ti2.spriteImportMode = SpriteImportMode.Multiple;

        List<SpriteMetaData> newData = new List<SpriteMetaData>();

        // Debug.Log("Amount of slices found: " + ti1.spritesheet.Length);
        // Debug.Log("dest.name = " + dest.name);

        for (int i = 0; i < ti1.spritesheet.Length; i++)
        {
            SpriteMetaData meta = ti1.spritesheet[i];
            meta.name = dest.name + "_" + i;
            newData.Add(meta);
        }

        ti2.spritesheet = newData.ToArray();
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

    }
}


