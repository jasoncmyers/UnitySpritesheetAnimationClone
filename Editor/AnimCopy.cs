using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class AnimCopy : EditorWindow
{

    AnimationClip sourceAnim;
    AnimationClip destAnim;
    Texture2D sourceSheet;
    // Texture2D destSheet;
        
    private Texture2D[] spriteSheetsToApply;
    private string[] outputAnimationNames;
    private int numDestSheets = 1;
    private Texture2D[] destSheets = { };
    private int numSourceAnims = 1;
    private AnimationClip[] sourceAnims = { };
    private string oldPrefix;
    private string[] newPrefix = { };
        

    // Creates a new option in "Windows"
    [MenuItem("Window/Clone animation to new spritesheet")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        AnimCopy window = (AnimCopy)EditorWindow.GetWindow(typeof(AnimCopy), false, "Animation Clone");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Space(25f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Original Animation:", EditorStyles.boldLabel);
        sourceAnim = (AnimationClip)EditorGUILayout.ObjectField(sourceAnim, typeof(AnimationClip), false, GUILayout.Width(220));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Original Spritesheet:", EditorStyles.boldLabel);
        sourceSheet = (Texture2D)EditorGUILayout.ObjectField(sourceSheet, typeof(Texture2D), false, GUILayout.Width(220));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Prefix to replace:", EditorStyles.boldLabel);
        oldPrefix = EditorGUILayout.TextField(oldPrefix, GUILayout.Width(220));
        GUILayout.EndHorizontal();

        GUILayout.Space(25f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Number of spitesheets to apply:", EditorStyles.boldLabel);
        //numDestSheets = EditorGUILayout.IntField(numDestSheets, GUILayout.Width(220));
        numDestSheets = EditorGUILayout.IntSlider(numDestSheets, 1, 10, GUILayout.Width(220));
        GUILayout.EndHorizontal();

        /*GUILayout.BeginHorizontal();
        GUILayout.Label("Destination Spritesheet:", EditorStyles.boldLabel);
        destSheet = (Texture2D)EditorGUILayout.ObjectField(destSheet, typeof(Texture2D), false, GUILayout.Width(220));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Prefix for new animation:", EditorStyles.boldLabel);
        newPrefix = EditorGUILayout.TextField(newPrefix, GUILayout.Width(220));
        GUILayout.EndHorizontal();*/

        /* GUILayout.BeginHorizontal();
        GUILayout.Label("Source animations:", EditorStyles.boldLabel);
        GUILayout.EndHorizontal();
        for (int i = 0; i < numSourceAnims; i++)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(sourceAnims[i], typeof(AnimationClip), false, GUILayout.Width(220));
            GUILayout.EndHorizontal();
        } */

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
            System.Array.Copy(newPrefix, prefix_temp, entriesToCopy);
            destSheets = temp;
            newPrefix = prefix_temp;
        }
        /* TODO: implement for multiple animations and sheets at once
        if (sourceAnims.Length != numSourceAnims)
        {
            AnimationClip[] temp = new AnimationClip[numSourceAnims];
            sourceAnims.CopyTo(temp, 0);
            sourceAnims = temp;
        } */

        for (int i = 0; i < numDestSheets; i++)
        {
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Destination Spritesheet:", EditorStyles.boldLabel);
            destSheets[i] = (Texture2D)EditorGUILayout.ObjectField(destSheets[i], typeof(Texture2D), false, GUILayout.Width(220));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Prefix for new animation:", EditorStyles.boldLabel);
            newPrefix[i] = EditorGUILayout.TextField(newPrefix[i], GUILayout.Width(220));
            GUILayout.EndHorizontal();
        }


        GUILayout.Space(25f);
        if (GUILayout.Button("Copy animation using new spritesheet"))
        {
            CopyAnimationToNewSheet(sourceSheet, sourceAnim, destSheets);
        }
    }


    private void CopyAnimationToNewSheet(Texture2D sourceSheet, AnimationClip sourceAnim, Texture2D[] destSheets)
    {

        // Error checking stuff.  Deal with this later, after it's working with known good data
        /* if (!sourceAnim || !destAnim || !sourceSheet || !destSheet)
        {
            Debug.Log("Missing at least one object");
            return;
        }

        if (sourceSheet.GetType() != typeof(Texture2D) || destSheet.GetType() != typeof(Texture2D))
        {
            Debug.Log("Both spritesheets must be Texture2D objects.");
            return;
        } */


        for (int i = 0; i < destSheets.Length; i++)
        {
            Texture2D destSheet = destSheets[i];
            if (destSheet == null) continue;

            CopySpritesheetSlices(sourceSheet, destSheet);
            string sourceGuid = ReadSpritesIDsFromSheetMeta(sourceSheet, out string[] sourceSpriteIDs);
            int[] animSpriteNums = GetSpriteNumbersFromAnimation(sourceSheet, sourceAnim);
            string destGuid = ReadSpritesIDsFromSheetMeta(destSheet, out string[] destSpriteIDs);

            string sourcePath = AssetDatabase.GetAssetPath(sourceAnim);
            string dest = (new Regex(oldPrefix)).Replace(sourcePath, newPrefix[i]);
            if (dest == sourcePath) dest = "Assets/blank_copy.anim";

            bool worked = AssetDatabase.CopyAsset(sourcePath, dest);
            string animFile = File.ReadAllText(dest);
            string newAnimFile = animFile;
            for (int j = 0; j < animSpriteNums.Length; j++)
            {
                string replaceGuid = "value: {fileID: " + sourceSpriteIDs[j] + ", guid: " + sourceGuid + ",";
                string newGuidString = "value: {fileID: " + destSpriteIDs[j] + ", guid: " + destGuid + ",";
                var regexReplace = new Regex(replaceGuid);
                newAnimFile = regexReplace.Replace(newAnimFile, newGuidString);
            }

            File.WriteAllText(dest, newAnimFile);
            AssetDatabase.Refresh();
        }
        
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


