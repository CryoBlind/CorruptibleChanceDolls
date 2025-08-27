using BepInEx;
using BepInEx.Configuration;
using R2API;
using RoR2;
using RoR2.Audio;
using RoR2.ContentManagement;
using RoR2.ExpansionManagement;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CorruptibleChanceDolls
{
    // This is an example plugin that can be put in
    // BepInEx/plugins/ExamplePlugin/ExamplePlugin.dll to test out.
    // It's a small plugin that adds a relatively simple item to the game,
    // and gives you that item whenever you press F2.

    // This attribute specifies that we have a dependency on a given BepInEx Plugin,
    // We need the R2API ItemAPI dependency because we are using for adding our item to the game.
    // You don't need this if you're not using R2API in your plugin,
    // it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(ItemAPI.PluginGUID)]

    // This one is because we use a .language file for language tokens
    // More info in https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Assets/Localization/
    [BepInDependency(LanguageAPI.PluginGUID)]

    // This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    // This is the main declaration of our plugin class.
    // BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    // BaseUnityPlugin itself inherits from MonoBehaviour,
    // so you can use this as a reference for what you can declare and use in your plugin class
    // More information in the Unity Docs: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class CorruptibleChanceDolls : BaseUnityPlugin
    {
        // The Plugin GUID should be a unique ID for this plugin,
        // which is human readable (as it is used in places like the config).
        // If we see this PluginGUID as it is on thunderstore,
        // we will deprecate this mod.
        // Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "CryoBlind";
        public const string PluginName = "CorruptibleChanceDolls";
        public const string PluginVersion = "1.0.3";

        // We need our item definition to persist through our functions, and therefore make it a class field.
        private static ItemDef CorruptedChanceDoll;
        private static GameObject chanceSuccessEffect;

        private static ConfigFile CorruptedChanceDollsConfig { get; set; }
        public static ConfigEntry<float> ExtraItemBaseChance { get; set; }
        public static ConfigEntry<float> ExtraItemStackScale { get; set; }
        public static ConfigEntry<float> ExtraItemMaxChance { get; set; }
        public static PluginInfo PInfo { get; private set; }
        // The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            CorruptedChanceDollsConfig = new ConfigFile(Paths.ConfigPath + "\\CorruptibleChanceDolls.cfg", true);

            ExtraItemBaseChance = CorruptedChanceDollsConfig.Bind<float>(
                "Base Chance For Extra Item",
                "ExtraItemBaseChance",
                        15f,
                "This is how likely the corrupted chance doll is to proc with a stack of only 1 in the player's inventory"
            );
            ExtraItemStackScale = CorruptedChanceDollsConfig.Bind<float>(
                "Scaling Factor",
                "ExtraItemStackScale",
                        0.5f,
                "Dictates how fast the item scales with additional stacks"
            );
            ExtraItemMaxChance = CorruptedChanceDollsConfig.Bind<float>(
                "Maximum Chance For Extra Item",
                "ExtraItemMaxChance",
                        80f,
                "The chance of getting an extra item is clamped below this value"
            );


            PInfo = Info;
            // Init our logging class so that we can properly log for debugging
            Log.Init(Logger);
            Asset.Init();

            // First let's define our item
            CorruptedChanceDoll = ScriptableObject.CreateInstance<ItemDef>();

            // Language Tokens, explained there https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Assets/Localization/
            CorruptedChanceDoll.name = "CORRUPTED_CHANCE_DOLL";
            CorruptedChanceDoll.nameToken = "Chance Effigy";
            CorruptedChanceDoll.pickupToken = "The power of voodoo, who do, you do";
            CorruptedChanceDoll.descriptionToken = "Chance for an Chance Shrine drop to not be counted towards the total drops from that chance shrine.  Scales according to: % chance = " + ExtraItemBaseChance.Value + " + " + ExtraItemBaseChance.Value + "(x-1)^" + ExtraItemStackScale.Value + " for x >= 1 up to " + ExtraItemMaxChance.Value + "% chance";
            CorruptedChanceDoll.loreToken = "Chance for an Chance Shrine drop to not be counted towards the total drops from that chance shrine.  Scales according to: % chance = " + ExtraItemBaseChance.Value + " + " + ExtraItemBaseChance.Value + "(x-1)^" + ExtraItemStackScale.Value + " for x >= 1 up to " + ExtraItemMaxChance.Value + "% chance";


            // The tier determines what rarity the item is:
            // Tier1=white, Tier2=green, Tier3=red, Lunar=Lunar, Boss=yellow,
            // and finally NoTier is generally used for helper items, like the tonic affliction
#pragma warning disable Publicizer001 // Accessing a member that was not originally public. Here we ignore this warning because with how this example is setup we are forced to do this
            CorruptedChanceDoll._itemTierDef = Addressables.LoadAssetAsync<ItemTierDef>("RoR2/DLC1/Common/VoidTier2Def.asset").WaitForCompletion();
#pragma warning restore Publicizer001
            // Instead of loading the itemtierdef directly, you can also do this like below as a workaround
            // myItemDef.deprecatedTier = ItemTier.Tier2;

            // You can create your own icons and prefabs through assetbundles, but to keep this boilerplate brief, we'll be using question marks.
            //CorruptedChanceDoll.pickupIconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/DLC2/Items/ExtraShrineItem/texChanceDollIcon.png").WaitForCompletion();
            //CorruptedChanceDoll.pickupModelPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC2/Items/ExtraShrineItem/PickupChanceDoll.prefab").WaitForCompletion();
            CorruptedChanceDoll.pickupIconSprite = Asset.mainBundle.LoadAsset<Sprite>("texCorruptedChanceDollIco.png");
            CorruptedChanceDoll.pickupModelPrefab = Asset.mainBundle.LoadAsset<GameObject>("PickupCorruptedChanceDoll.prefab");

            // Can remove determines
            // if a shrine of order,
            // or a printer can take this item,
            // generally true, except for NoTier items.
            CorruptedChanceDoll.canRemove = true;

            // Hidden means that there will be no pickup notification,
            // and it won't appear in the inventory at the top of the screen.
            // This is useful for certain noTier helper items, such as the DrizzlePlayerHelper.
            CorruptedChanceDoll.hidden = false;

            // You can add your own display rules here,
            // where the first argument passed are the default display rules:
            // the ones used when no specific display rules for a character are found.
            // For this example, we are omitting them,
            // as they are quite a pain to set up without tools like https://thunderstore.io/package/KingEnderBrine/ItemDisplayPlacementHelper/
            var displayRules = new ItemDisplayRuleDict(null);

            //manage content pack
            var contentPack = new ContentPack();


            //manage corrupting vanilla chance doll
            ItemDef chanceDoll = Addressables.LoadAssetAsync<ItemDef>("RoR2/DLC2/Items/ExtraShrineItem/ExtraShrineItem.asset").WaitForCompletion();

            var relationshipProvider = ScriptableObject.CreateInstance<ItemRelationshipProvider>();
            relationshipProvider.name = "CorruptedChanceDollRelationshipProvider";
            relationshipProvider.relationshipType = Addressables.LoadAssetAsync<ItemRelationshipType>("RoR2/DLC1/Common/ContagiousItem.asset").WaitForCompletion();
            relationshipProvider.relationships = 
            [
                new ItemDef.Pair
                {
                    itemDef1 = chanceDoll,
                    itemDef2 = CorruptedChanceDoll
                }
            ];

            // Then finally add it to R2API
            ItemAPI.Add(new CustomItem(CorruptedChanceDoll, displayRules));
            ContentAddition.AddItemRelationshipProvider(relationshipProvider);

            //load Effect Prefab
            chanceSuccessEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidChest/VoidChestPurchaseEffect.prefab").WaitForCompletion();

            // But now we have defined an item, but it doesn't do anything yet. So we'll need to define that ourselves
            On.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;

            //hook onto voidtransformation initilization to add DLC dependency
            On.RoR2.ExpansionManagement.ExpansionCatalog.Init += ExpansionCatalog_Init;
        }

        private void ExpansionCatalog_Init(On.RoR2.ExpansionManagement.ExpansionCatalog.orig_Init orig)
        {
            orig();
            CorruptedChanceDoll.requiredExpansion = ExpansionCatalog.expansionDefs.Where(expansionDef => expansionDef.nameToken == "DLC1_NAME").FirstOrDefault();
        }

        private static void PurchaseInteraction_OnInteractionBegin(On.RoR2.PurchaseInteraction.orig_OnInteractionBegin orig, PurchaseInteraction self, Interactor activator)
        {
            
            var shrineChanceComponent = self.gameObject.GetComponent<ShrineChanceBehavior>();
            
            if (shrineChanceComponent != null)
            {
                var interactorPlayer = activator.gameObject.GetComponent<CharacterBody>();
                int successesBeforePurchase = shrineChanceComponent.successfulPurchaseCount;

                orig(self, activator);

                if (successesBeforePurchase < shrineChanceComponent.successfulPurchaseCount)
                {
                    int itemCount = interactorPlayer.inventory.GetItemCount(CorruptedChanceDoll.itemIndex);
                    if(itemCount > 0 && Util.CheckRoll(Mathf.Clamp(ExtraItemBaseChance.Value + ExtraItemBaseChance.Value * Mathf.Pow(itemCount-1, ExtraItemStackScale.Value), 0, ExtraItemMaxChance.Value), interactorPlayer.master))
                    {
                        //passed all checks
                        shrineChanceComponent.successfulPurchaseCount--; //make purchase not count

                        //show some success effect
                        EffectManager.SpawnEffect(chanceSuccessEffect, new EffectData
                        {
                            origin = self.gameObject.transform.position + new Vector3(0f, 2.5f, 0f),
                            rotation = Quaternion.identity,
                            scale = 2.5f
                        }, true);
                    }
                }
            }
            else orig(self, activator);
        }

        //The Update() method is run on every frame of the game.
        //private void Update()
        //{
        //    // This if statement checks if the player has currently pressed F2.
        //    if (Input.GetKeyDown(KeyCode.F2))
        //    {
        //        // Get the player body to use a position:
        //        var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

        //        // And then drop our defined item in front of the player.

        //        Log.Info($"Player pressed F2. Spawning our custom item at coordinates {transform.position}");
        //        PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(CorruptedChanceDoll.itemIndex), transform.position, transform.forward * 20f);
        //    }
        //    if (Input.GetKeyDown(KeyCode.F3))
        //    {
        //        // Get the player body to use a position:
        //        var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

        //        // And then drop our defined item in front of the player.

        //        Log.Info($"Player pressed F2. Spawning our custom item at coordinates {transform.position}");
        //        PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(DLC2Content.Items.ExtraShrineItem.itemIndex), transform.position, transform.forward * 20f);
        //    }
        //    if (Input.GetKeyDown(KeyCode.F4))
        //    {
        //        // Get the player body to use a position:
        //        var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

        //        EffectManager.SpawnEffect(chanceSuccessEffect, new EffectData
        //        {
        //            origin = transform.position + new Vector3(0f, 2.5f, 0f),
        //            rotation = Quaternion.identity,
        //            scale = 2.5f
        //        }, true);
        //    }
        //}
    }

    //Static class for ease of access
    public static class Asset
    {
        //You will load the assetbundle and assign it to here.
        public static AssetBundle mainBundle;
        //A constant of the AssetBundle's name.
        public const string bundleName = "corruptiblechancedollassets";
        // Uncomment this if your assetbundle is in its own folder. Of course, make sure the name of the folder matches this.
        //public const string assetBundleFolder = "Assets";

        //The direct path to your AssetBundle
        public static string AssetBundlePath
        {
            get
            {
                //This returns the path to your assetbundle assuming said bundle is on the same folder as your DLL. If you have your bundle in a folder, you can instead uncomment the statement below this one.
                return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(CorruptibleChanceDolls.PInfo.Location), bundleName);
                //return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(CorruptibleChanceDolls.PInfo.Location), assetBundleFolder, bundleName);
            }
        }

        public static void Init()
        {
            //Loads the assetBundle from the Path, and stores it in the static field.
            mainBundle = AssetBundle.LoadFromFile(AssetBundlePath);
        }
    }
}

