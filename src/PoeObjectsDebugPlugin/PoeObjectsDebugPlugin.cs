using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PoeHUD.Plugins;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.EntityComponents;
using System.IO;
using SharpDX;
using SharpDX.Direct3D9;
using PoeHUD.Poe;
using PoeHUD.Poe.Elements;
using System.Reflection;
using PoeHUD.Poe.RemoteMemoryObjects;
using PoeHUD.Poe.FilesInMemory;
using PoeHUD.Models;

namespace PoeObjectsDebugPlugin
{
    public class PoeObjectsDebugPlugin : BaseSettingsPlugin<PoeObjectsDebugPlugin_Settings>
    {
        private const string DIVIDER = "—";
        private const BindingFlags setPropFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private Dictionary<string, Type> PoeHUDComponents = new Dictionary<string, Type>();

        public override void Initialise()
        {
            var poeHudTypes = typeof(Life).Assembly.GetTypes();
            foreach(var type in poeHudTypes)
            {
                if (!type.FullName.Contains("PoeHUD.Poe.EntityComponents") && !type.FullName.Contains("PoeHUD.Poe.Components")) continue;
                PoeHUDComponents.Add(type.Name, type);
            }

            Settings.ObjectDebugList.Value = "";
            Settings.ObjectDebugList.OnValueSelected += DebugObjectSelected;
        }

        private void DebugObjectSelected(string value)
        {
            Settings.ObjectDebugList.OnValueSelected -= DebugObjectSelected;
            Settings.ObjectDebugList.Value = "";
            Settings.ObjectDebugList.OnValueSelected += DebugObjectSelected;

            var obj = ObjectsToDebug.Find(x => x.Path.Substring(x.Path.LastIndexOf('/')) + x.Address.ToString("x") == value);

            if(obj != null)
            {
                DebugItem(obj.InternalEntity);
            }
        }

        private List<EntityWrapper> ObjectsToDebug = new List<EntityWrapper>();
        private List<EntityWrapper> AllObjects = new List<EntityWrapper>();

        public override void EntityAdded(EntityWrapper entityWrapper)
        {
            if (!Settings.Enable.Value) return;
            AllObjects.Add(entityWrapper);
        }

        public override void EntityRemoved(EntityWrapper entityWrapper)
        {
            if (!Settings.Enable.Value) return;
            AllObjects.Remove(entityWrapper);
        }

        private List<string> ItemDebugLines = new List<string>();
        public override void Render()
        {
            var window = GameController.Window.GetWindowRectangle();
            Vector2 drawPos = window.TopLeft;
            window.Width = 400;
            window.Height = ItemDebugLines.Count * 17;

            Graphics.DrawBox(window, new Color(0, 0, 0, 200));
               
            foreach (var line in ItemDebugLines.ToList())
            {
                Graphics.DrawText(line, 15, drawPos, FontDrawFlags.Left);
                drawPos.Y += 17;
            }

            foreach(var obj in ObjectsToDebug)
            {
                var camPos = GameController.Game.IngameState.Camera.WorldToScreen(obj.Pos, obj);
                Graphics.DrawText(obj.Address.ToString("x"), 15, camPos);
            }

            if(Settings.ObjectDebug.PressedOnce())
            {
                var playerPos = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Positioned>().GridPos;
                ObjectsToDebug = AllObjects.Where(x => Vector2.Distance(x.GetComponent<Positioned>().GridPos, playerPos) < Settings.DetectDist.Value).ToList();

                Settings.ObjectDebugList.SetListValues(ObjectsToDebug.Select(x => x.Path.Substring(x.Path.LastIndexOf('/')) + x.Address.ToString("x")).ToList());
            }

            if (Settings.ItemDebug.PressedOnce())
            {
                ItemDebugLines = new List<string>();
                var hover = GameController.Game.IngameState.UIHover;
                if (hover != null && hover.Address != 0)
                {
                    var inventItem = hover.AsObject<NormalInventoryItem>();
                    var item = inventItem.Item;

                    if (item != null)
                        DebugItem(item);
                }
            }
        }

        private void DebugItem(Entity itemEntity)
        {
            ItemDebugLines = new List<string>();
            LogMessage(itemEntity.Path, 0);

            var components = itemEntity.GetComponents();
            var game = GameController.Game;
            ItemDebugLines.Add("Path: " + itemEntity.Path);
            ItemDebugLines.Add("Address: " + itemEntity.Address.ToString("x"));

            BaseItemType BIT = GameController.Files.BaseItemTypes.Translate(itemEntity.Path);

            if (BIT != null)
            {
                ItemDebugLines.Add("===== BaseItemTypeInfo: ======");
                DebugProperty(typeof(BaseItemType), BIT, 1);
                ItemDebugLines.Add("==============================");
            }

            foreach (var component in components)
            {
                ItemDebugLines.Add("");

                Type compType = null;
                if (!PoeHUDComponents.TryGetValue(component.Key, out compType))
                {
                    ItemDebugLines.Add($"<{component.Key}> (Not implemented in PoeHUD)");
                    ItemDebugLines.Add($"{DIVIDER}Address: {component.Value.ToString("x")}");
                    continue;
                }
                ItemDebugLines.Add($"<{component.Key}>:");
                object instance = Activator.CreateInstance(compType);

                var addrProp = compType.GetProperty("Address", setPropFlags);
                addrProp.SetValue(instance, component.Value);

                var gameProp = compType.GetProperty("Game", setPropFlags);
                gameProp.SetValue(instance, game);

                var mamoryProp = compType.GetProperty("M", setPropFlags);
                mamoryProp.SetValue(instance, Memory);


                DebugProperty(compType, instance, 1);
            }

            var path = Path.Combine(PluginDirectory, "_ItemDebugInfo.txt");
            File.WriteAllText(path, string.Join(Environment.NewLine, ItemDebugLines.ToArray()).Replace(DIVIDER, "\t"));
        }

        private void DebugProperty(Type type, object instance, int deph)
        {
            string prefix = "";
            for (int i = 0; i < deph; ++i)
                prefix += DIVIDER;

            var props = type.GetProperties();
            foreach (var prop in props)
            {
                object value = null;
                Exception Ex = null;
                try
                {
                    value = prop.GetValue(instance);
                }
                catch (Exception ex)
                { Ex = ex; }

                DrawProperty(value, deph, prefix, prop.Name, Ex);
            }

            var fields = type.GetFields();
            foreach (var field in fields)
            {
                object value = null;
                Exception Ex = null;
                try
                {
                    value = field.GetValue(instance);
                }
                catch (Exception ex)
                { Ex = ex; }

                DrawProperty(value, deph, prefix, field.Name, Ex);
            }
        }

        private void DrawProperty(object value, int deph, string prefix, string propertyName, Exception ex = null)
        {
            if(value == null)
            {
                if(ex != null)
                    ItemDebugLines.Add($"{prefix}{propertyName} : %Error: " + ex.Message);
                else
                    ItemDebugLines.Add($"{prefix}{propertyName} : NULL");
                return;
            }
            var propType = value.GetType();

            if (propType.IsEnum)
            {
                ItemDebugLines.Add($"{prefix}{propertyName} : {value}");
            }
            else if (propType.IsPrimitive || propType == typeof(string))
            {
                string strValue;
                if (propertyName == "Address")
                {
                    long addr = (long)(value);
                    strValue = addr.ToString("x");
                }
                else
                {
                    strValue = value.ToString();
                }

                ItemDebugLines.Add($"{prefix}{propertyName} : {strValue}");
            }
            else if (propType.GetInterface("IEnumerable") != null)
            {
                ItemDebugLines.Add($"{prefix}{propertyName} ({GetFullName(propType)}) :");

                int index = 0;
                foreach (var listitem in value as IEnumerable)
                {
                    DrawProperty(listitem, deph + 1, prefix + DIVIDER, $"Index_{index}");
                    index++;
                }
            }
            else if (propType.IsClass)
            {
                ItemDebugLines.Add($"{prefix}{propertyName} : ");
                DebugProperty(propType, value, deph + 1);

                if (Settings.TranslateModsFromFiles.Value)
                {
                    if (propType == typeof(ItemMod))
                    {
                        var rawNameProp = propType.GetProperty("RawName");

                        if (rawNameProp != null)//Should be never null
                        {
                            string rawName = (string)rawNameProp.GetValue(value);
                            ModsDat.ModRecord record;

                            if (GameController.Files.Mods.records.TryGetValue(rawName, out record))
                            {
                                ItemDebugLines.Add($"{prefix}=== ModRecord FromFiles : ===");
                                DebugProperty(typeof(ModsDat.ModRecord), record, deph + 2);
                                ItemDebugLines.Add($"{prefix}=============================");
                            }
                        }
                    }
                }
            }
            else if (propType == typeof(Vector3))
            {
                var vect = (Vector3)value;
                ItemDebugLines.Add($"{prefix}{propertyName} : Vector3:{vect.X} {vect.Y} {vect.Z}");
            }
            else if (propType == typeof(Vector2))
            {
                var vect = (Vector2)value;
                ItemDebugLines.Add($"{prefix}{propertyName} : Vector2:{vect.X} {vect.Y}");
            }
            else
            {
                ItemDebugLines.Add($"{prefix}{propertyName} : Parser is not defined for type of Type: {propType}");
            }
        }

        static string GetFullName(Type t)
        {
            if (!t.IsGenericType)
                return t.Name;
            StringBuilder sb = new StringBuilder();

            sb.Append(t.Name.Substring(0, t.Name.LastIndexOf("`")));
            sb.Append(t.GetGenericArguments().Aggregate("<",

                delegate (string aggregate, Type type)
                {
                    return aggregate + (aggregate == "<" ? "" : ",") + GetFullName(type);
                }
                ));
            sb.Append(">");

            return sb.ToString();
        }
    }
}
