using System.Text;

namespace FileSorting.Generator;

public sealed class DictionaryStringPool
{
    private readonly byte[][] _words;

    private DictionaryStringPool(byte[][] words)
    {
        _words = words;
    }

    public int Count => _words.Length;

    public byte[] GetString(int index) => _words[index];

    public static DictionaryStringPool CreateDefault()
    {
        var words = DefaultWords.Select(w => Encoding.UTF8.GetBytes(w)).ToArray();
        return new DictionaryStringPool(words);
    }

    public static DictionaryStringPool FromFile(string path)
    {
        var lines = File.ReadAllLines(path);
        var words = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Encoding.UTF8.GetBytes(line.Trim()))
            .ToArray();

        return words.Length == 0
            ? throw new InvalidOperationException($"Dictionary file '{path}' contains no valid entries")
            : new DictionaryStringPool(words);
    }

    private static readonly string[] DefaultWords =
    [
        // Common nouns
        "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape", "Honeydew",
        "Kiwi", "Lemon", "Mango", "Nectarine", "Orange", "Papaya", "Quince", "Raspberry",
        "Strawberry", "Tangerine", "Watermelon", "Apricot", "Blackberry", "Blueberry",
        "Cantaloupe", "Coconut", "Cranberry", "Dragonfruit", "Guava", "Jackfruit",
        "Lime", "Lychee", "Mulberry", "Passionfruit", "Peach", "Pear", "Persimmon",
        "Pineapple", "Plum", "Pomegranate", "Starfruit", "Avocado", "Grapefruit",

        // Animals
        "Lion", "Tiger", "Bear", "Wolf", "Fox", "Deer", "Rabbit", "Squirrel",
        "Eagle", "Hawk", "Falcon", "Owl", "Sparrow", "Robin", "Cardinal", "Blue jay",
        "Dolphin", "Whale", "Shark", "Octopus", "Jellyfish", "Starfish", "Seahorse",
        "Elephant", "Giraffe", "Zebra", "Hippo", "Rhino", "Gorilla", "Chimpanzee",
        "Kangaroo", "Koala", "Panda", "Penguin", "Polar bear", "Seal", "Walrus",
        "Crocodile", "Alligator", "Snake", "Lizard", "Turtle", "Frog", "Salamander",

        // Colors
        "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Pink", "Brown",
        "Black", "White", "Gray", "Silver", "Gold", "Bronze", "Copper", "Platinum",
        "Crimson", "Scarlet", "Maroon", "Coral", "Salmon", "Peach", "Amber", "Mustard",
        "Olive", "Lime", "Teal", "Cyan", "Navy", "Indigo", "Violet", "Magenta",
        "Lavender", "Plum", "Burgundy", "Chocolate", "Tan", "Beige", "Ivory", "Charcoal",

        // Weather and nature
        "Sunshine", "Moonlight", "Starlight", "Rainbow", "Thunder", "Lightning", "Rain",
        "Snow", "Fog", "Mist", "Cloud", "Storm", "Hurricane", "Tornado", "Blizzard",
        "Mountain", "Valley", "River", "Lake", "Ocean", "Sea", "Beach", "Desert",
        "Forest", "Jungle", "Meadow", "Prairie", "Canyon", "Cliff", "Waterfall",
        "Volcano", "Island", "Peninsula", "Glacier", "Reef", "Swamp", "Marsh",

        // Technology
        "Algorithm", "Database", "Network", "Server", "Client", "Protocol", "Interface",
        "Software", "Hardware", "Firmware", "Application", "Program", "System", "Platform",
        "Computer", "Laptop", "Tablet", "Smartphone", "Monitor", "Keyboard", "Mouse",
        "Processor", "Memory", "Storage", "Bandwidth", "Latency", "Throughput", "Cache",
        "Encryption", "Authentication", "Authorization", "Firewall", "Router", "Switch",
        "Virtual", "Container", "Microservice", "Framework", "Library", "Component",

        // Science
        "Atom", "Molecule", "Element", "Compound", "Reaction", "Catalyst", "Enzyme",
        "Protein", "Carbon", "Oxygen", "Hydrogen", "Nitrogen", "Electron", "Proton",
        "Neutron", "Photon", "Quantum", "Particle", "Wave", "Energy", "Matter", "Force",
        "Gravity", "Magnetism", "Electricity", "Radiation", "Spectrum", "Frequency",
        "Velocity", "Acceleration", "Momentum", "Inertia", "Friction", "Pressure",
        "Temperature", "Density", "Volume", "Mass", "Weight", "Distance", "Time",

        // Geography
        "Africa", "Antarctica", "Asia", "Australia", "Europe", "North America", "South America",
        "Atlantic", "Pacific", "Indian", "Arctic", "Mediterranean", "Caribbean", "Baltic",
        "Amazon", "Nile", "Mississippi", "Yangtze", "Ganges", "Danube", "Rhine", "Thames",
        "Everest", "Kilimanjaro", "Denali", "Matterhorn", "Fuji", "Vesuvius", "Etna",
        "Sahara", "Gobi", "Kalahari", "Atacama", "Mojave", "Arabian", "Patagonia",

        // Music
        "Symphony", "Concerto", "Sonata", "Opera", "Ballet", "Orchestra", "Ensemble",
        "Piano", "Violin", "Guitar", "Drums", "Flute", "Trumpet", "Saxophone", "Cello",
        "Melody", "Harmony", "Rhythm", "Tempo", "Pitch", "Tone", "Scale", "Chord",
        "Crescendo", "Diminuendo", "Staccato", "Legato", "Forte", "Piano", "Allegro",

        // Architecture
        "Building", "Tower", "Bridge", "Cathedral", "Castle", "Palace", "Temple",
        "Pyramid", "Dome", "Arch", "Column", "Pillar", "Foundation", "Facade", "Atrium",
        "Balcony", "Terrace", "Courtyard", "Garden", "Fountain", "Sculpture", "Monument",

        // Literature
        "Novel", "Poetry", "Drama", "Comedy", "Tragedy", "Epic", "Sonnet", "Haiku",
        "Metaphor", "Simile", "Allegory", "Symbolism", "Irony", "Paradox", "Hyperbole",
        "Narrative", "Character", "Plot", "Theme", "Setting", "Conflict", "Resolution",

        // Sports
        "Football", "Basketball", "Baseball", "Soccer", "Tennis", "Golf", "Hockey",
        "Swimming", "Running", "Cycling", "Boxing", "Wrestling", "Gymnastics", "Skating",
        "Skiing", "Surfing", "Volleyball", "Rugby", "Cricket", "Badminton", "Table tennis",

        // Food
        "Breakfast", "Lunch", "Dinner", "Dessert", "Appetizer", "Entree", "Salad", "Soup",
        "Bread", "Pasta", "Rice", "Potato", "Tomato", "Onion", "Garlic", "Pepper",
        "Chicken", "Beef", "Pork", "Fish", "Shrimp", "Lobster", "Crab", "Salmon",
        "Cheese", "Butter", "Cream", "Yogurt", "Milk", "Egg", "Flour", "Sugar",
        "Salt", "Olive oil", "Vinegar", "Mustard", "Ketchup", "Mayonnaise", "Soy sauce",

        // Professions
        "Doctor", "Nurse", "Teacher", "Engineer", "Scientist", "Lawyer", "Judge",
        "Architect", "Artist", "Writer", "Musician", "Actor", "Director", "Producer",
        "Chef", "Baker", "Farmer", "Fisherman", "Carpenter", "Plumber", "Electrician",
        "Pilot", "Captain", "Sailor", "Driver", "Mechanic", "Technician", "Developer",

        // Emotions
        "Happiness", "Sadness", "Anger", "Fear", "Surprise", "Disgust", "Joy", "Love",
        "Hope", "Peace", "Calm", "Excitement", "Enthusiasm", "Passion", "Courage",
        "Confidence", "Gratitude", "Kindness", "Compassion", "Empathy", "Patience",

        // Time
        "Second", "Minute", "Hour", "Day", "Week", "Month", "Year", "Decade", "Century",
        "Morning", "Afternoon", "Evening", "Night", "Midnight", "Noon", "Dawn", "Dusk",
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
        "January", "February", "March", "April", "May", "June", "July", "August",
        "September", "October", "November", "December", "Spring", "Summer", "Autumn", "Winter",

        // Materials
        "Wood", "Metal", "Glass", "Plastic", "Rubber", "Leather", "Cotton", "Silk",
        "Stone", "Marble", "Granite", "Concrete", "Brick", "Steel", "Iron", "Aluminum",
        "Copper", "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Pearl", "Crystal",

        // Transportation
        "Car", "Bus", "Train", "Plane", "Boat", "Ship", "Bicycle", "Motorcycle",
        "Helicopter", "Submarine", "Rocket", "Spaceship", "Truck", "Van", "Taxi", "Tram",

        // Buildings
        "House", "Apartment", "Office", "School", "Hospital", "Library", "Museum", "Theater",
        "Restaurant", "Hotel", "Airport", "Station", "Factory", "Warehouse", "Stadium",

        // Phrases for variety
        "Quick brown fox", "Lazy dog", "Hello world", "Good morning", "Good evening",
        "Best wishes", "Kind regards", "Thank you", "Please wait", "Coming soon",
        "New arrival", "Special offer", "Limited edition", "Premium quality", "Top rated",
        "Customer favorite", "Best seller", "Trending now", "Most popular", "Editor choice",
        "Highly recommended", "Award winning", "Industry leading", "Cutting edge", "State of the art",
        "Next generation", "Revolutionary design", "Innovative solution", "Breakthrough technology",
        "Performance optimized", "Energy efficient", "Eco friendly", "Sustainable choice",
        "Handcrafted quality", "Artisan made", "Locally sourced", "Organic certified",
        "Fair trade", "Cruelty free", "Vegan friendly", "Gluten free", "Sugar free",

        // Additional common words
        "Adventure", "Balance", "Challenge", "Discovery", "Excellence", "Freedom", "Growth",
        "Harmony", "Innovation", "Journey", "Knowledge", "Leadership", "Motivation",
        "Opportunity", "Progress", "Quality", "Resilience", "Success", "Transformation",
        "Understanding", "Vision", "Wisdom", "Achievement", "Breakthrough", "Creativity",
        "Determination", "Efficiency", "Flexibility", "Guidance", "Integrity", "Justice",

        // More technical terms
        "Binary", "Boolean", "Byte", "Character", "Decimal", "Double", "Float", "Integer",
        "String", "Array", "List", "Dictionary", "Queue", "Stack", "Tree", "Graph",
        "Hash", "Sort", "Search", "Filter", "Map", "Reduce", "Parse", "Serialize",
        "Compile", "Debug", "Deploy", "Execute", "Initialize", "Iterate", "Validate",

        // Business terms
        "Strategy", "Analysis", "Planning", "Marketing", "Finance", "Operations", "Management",
        "Customer", "Product", "Service", "Revenue", "Profit", "Investment", "Budget",
        "Target", "Milestone", "Deadline", "Deliverable", "Stakeholder", "Partnership",

        // Abstract concepts
        "Infinity", "Eternity", "Possibility", "Reality", "Imagination", "Perception", "Consciousness",
        "Memory", "Identity", "Purpose", "Meaning", "Truth", "Beauty", "Wisdom", "Power",

        // More words to reach ~1000
        "Absolute", "Brilliant", "Captivating", "Delightful", "Elegant", "Fascinating", "Gorgeous",
        "Harmonious", "Impressive", "Jubilant", "Kinetic", "Luminous", "Magnificent", "Noble",
        "Outstanding", "Phenomenal", "Quintessential", "Radiant", "Spectacular", "Tremendous",
        "Ultimate", "Vibrant", "Wonderful", "Exquisite", "Youthful", "Zealous", "Adventurous",
        "Boundless", "Captivated", "Dazzling", "Enchanting", "Flourishing", "Glorious", "Heavenly",
        "Illuminating", "Joyful", "Kaleidoscope", "Legendary", "Mesmerizing", "Nurturing",
        "Optimistic", "Picturesque", "Quaint", "Refreshing", "Serene", "Tranquil", "Uplifting",
        "Versatile", "Wholesome", "Xenial", "Yearning", "Zenith", "Admirable", "Blissful",
        "Charismatic", "Dynamic", "Empowering", "Faithful", "Graceful", "Humble", "Inspiring",
        "Jovial", "Keen", "Lively", "Majestic", "Natural", "Original", "Peaceful", "Quirky",
        "Remarkable", "Sincere", "Thoughtful", "Unique", "Valuable", "Warm", "Expressive",

        // Compound phrases
        "Blue sky", "Green grass", "Red apple", "White snow", "Black night", "Golden sun",
        "Silver moon", "Crystal clear", "Deep blue", "Bright star", "Soft wind", "Gentle rain",
        "Warm summer", "Cool autumn", "Cold winter", "Fresh spring", "Early morning", "Late evening",
        "High mountain", "Deep valley", "Wide river", "Tall tree", "Small flower", "Big city",
        "Old town", "New world", "Fast train", "Slow boat", "Long road", "Short path",
        "Hard work", "Easy task", "Strong bond", "Weak link", "True friend", "False hope",
        "Real dream", "Pure heart", "Open mind", "Closed door", "First step", "Last chance",
        "Best day", "Worst case", "Good news", "Bad luck", "Right choice", "Wrong turn",
        "Great idea", "Small talk", "Big picture", "Fine print", "Main event", "Side effect",
        "Front page", "Back story", "Top secret", "Bottom line", "Left behind", "Right away",
        "Up ahead", "Down below", "In between", "Out there", "Over here", "Under cover",
        "Around corner", "Through tunnel", "Across bridge", "Along path", "Before dawn", "After sunset"
    ];
}
