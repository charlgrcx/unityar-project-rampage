using UnityEngine;

/// <summary>All the content of the app: items, feature info, and quiz questions.
/// Edit this file to change the text without touching any other script.</summary>
public static class GoBagData
{
    public const int PointsPerItem = 10;     // points for discovering an item
    public const int PointsPerAnswer = 20;   // points for a correct quiz answer

    public class Item
    {
        public string name, info, tip;
        public Item(string n, string i, string t) { name = n; info = i; tip = t; }
    }

    public class Question
    {
        public string text, explain;
        public string[] options;
        public int answer;
        public Question(string t, string[] o, int a, string e) { text = t; options = o; answer = a; explain = e; }
    }

    // ORDER MATTERS: the first entry is Item_0, the second is Item_1, and so on.
    // Item_0 clothes, Item_1 first-aid kit, Item_2 face mask,
    // Item_3 water bottle, Item_4 whistle, Item_5 flashlight
    public static readonly Item[] Items =
    {
        // Item_0
        new Item("Pack of Clothes",
            "Disasters can leave you wet, cold or stuck outside for hours. A spare set of clothes keeps you dry and warm, which helps prevent hypothermia and illness when you cannot get home to change.",
            "Add a light jacket or poncho, and keep everything in a sealed bag."),

        // Item_1
        new Item("First-Aid Kit",
            "Help can take hours or even days to arrive after a disaster. A first-aid kit lets you treat cuts, burns and other injuries right away, so they do not get worse or become infected.",
            "Include your personal medicines and check expiry dates twice a year."),

        // Item_2
        new Item("Face Mask",
            "Smoke, dust and debris fill the air after fires, earthquakes and volcanic ash. A mask filters these particles so you can breathe more safely and protect your lungs while you evacuate.",
            "Pack several (N95 type if possible) and keep them sealed."),

        // Item_3
        new Item("Water Bottle",
            "People can survive only a few days without water, and tap water may be cut off or unsafe in a disaster. Stored water keeps you hydrated, alert and strong enough to keep moving.",
            "Plan about 4 liters (1 gallon) per person per day and replace stored water every 6 months."),

        // Item_4
        new Item("Whistle",
            "If you are trapped or hurt, shouting wears you out fast. A whistle is louder, travels farther and never runs out of energy, so rescuers can find you. Three blasts is the universal distress signal.",
            "Clip it to a strap you can reach quickly."),

        // Item_5
        new Item("Flashlight",
            "Blackouts are common in disasters, and moving in the dark causes falls and injuries. A flashlight gives safe light with no fire risk, so you can see hazards, find your way and signal for help.",
            "Pack spare batteries. It is safer than candles."),
    };

    // Each question needs exactly 4 options. 'answer' is the position of the
    // correct option: 0 = first, 1 = second, 2 = third, 3 = fourth.
    public static readonly Question[] Quiz =
    {
        new Question("Why should you pack a spare change of clothes?",
            new[] { "It looks neat", "To stay dry and warm and avoid getting sick", "It makes the bag lighter", "To signal rescuers" }, 1,
            "Wet or cold clothes can lead to hypothermia and illness."),
        new Question("Why keep a first-aid kit in your go-bag?",
            new[] { "To treat injuries right away while waiting for help", "To make the bag heavier", "To replace drinking water", "To signal rescuers" }, 0,
            "Quick treatment stops injuries from getting worse or infected."),
        new Question("Why pack face masks?",
            new[] { "To keep food warm", "To filter dust and smoke in the air", "To light the way at night", "To call for help" }, 1,
            "Masks help protect your lungs when the air is polluted."),
        new Question("How much drinking water should you plan per person, per day?",
            new[] { "About 0.5 liter", "About 1 liter", "About 4 liters (1 gallon)", "About 10 liters" }, 2,
            "Roughly 4 liters covers drinking and basic hygiene."),
        new Question("Why is a whistle better than shouting for help?",
            new[] { "It is louder, travels farther and does not tire you out", "It plays music", "It boils water", "It scares away every danger" }, 0,
            "Three blasts signal distress and save your energy."),
        new Question("Why is a flashlight better than a candle in a blackout?",
            new[] { "It is cheaper", "It has no open flame or fire risk", "It never needs batteries", "It is always brighter" }, 1,
            "Candles can start fires. LED flashlights are much safer."),
    };
}