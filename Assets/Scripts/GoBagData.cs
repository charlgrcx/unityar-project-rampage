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

    // EARTHQUAKE QUIZ: one question about using each essential after an earthquake.
    // Each question needs exactly 4 options. 'answer' is the position of the
    // correct option: 0 = first, 1 = second, 2 = third, 3 = fourth.
    public static readonly Question[] Quiz =
    {
        // clothes
        new Question("After an earthquake you wait outside and it starts to rain. How does your spare set of clothes help?",
            new[] { "It makes the bag lighter", "It keeps you dry and warm so you do not get sick", "It stops the ground from shaking", "It calls the rescuers" }, 1,
            "Wet, cold clothes can make you sick. Dry clothes keep your body warm."),

        // first-aid kit
        new Question("The shaking stops and your friend has a small cut from broken glass. Which essential should you use?",
            new[] { "The whistle", "The face mask", "The first-aid kit", "The flashlight" }, 2,
            "A first-aid kit has bandages to clean and cover cuts. Always tell a grown-up too!"),

        // face mask
        new Question("After an earthquake, dust from broken walls fills the air. What should you do with your face mask?",
            new[] { "Wear it over your nose and mouth", "Use it as a pillow", "Hang it on your bag", "Save it for later" }, 0,
            "A mask filters dust so you can breathe safer and protect your lungs."),

        // water bottle
        new Question("An earthquake breaks the water pipes, and tap water may not be safe. Why do you need your water bottle?",
            new[] { "To wash the floor", "To make the shaking stop", "To water the plants", "To have safe water to drink" }, 3,
            "People need safe drinking water every day. Plan about 4 liters per person per day."),

        // whistle
        new Question("You are stuck behind a fallen cabinet and nobody can see you. How do you use your whistle?",
            new[] { "Blow a happy song", "Blow it in 3 loud blasts to call for help", "Throw it far away", "Hide it in your pocket" }, 1,
            "Three blasts is the signal for help. A whistle is louder than shouting and does not tire you out."),

        // flashlight
        new Question("An earthquake cuts the power at night. Why is a flashlight better than a candle?",
            new[] { "It has no flame, so it cannot start a fire", "It is hotter than a candle", "It makes a loud noise", "It uses water" }, 0,
            "After an earthquake there may be a gas leak. A flame could start a fire, so use a flashlight."),
    };
}
