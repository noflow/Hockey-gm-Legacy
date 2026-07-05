namespace LegacyEngine.Names;

public sealed record NamePool(
    NameOrigin Origin,
    string Nationality,
    IReadOnlyList<string> FirstNames,
    IReadOnlyList<string> LastNames,
    IReadOnlyList<string> Birthplaces)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Nationality))
        {
            throw new ArgumentException("Name pool nationality is required.", nameof(Nationality));
        }

        if (FirstNames.Count == 0 || LastNames.Count == 0 || Birthplaces.Count == 0)
        {
            throw new ArgumentException("Name pools require first names, last names, and birthplaces.");
        }

        if (FirstNames.Concat(LastNames).Any(value => string.IsNullOrWhiteSpace(value) || value.Any(char.IsDigit)))
        {
            throw new ArgumentException("Name pool values must be clean display text without numeric markers.");
        }
    }

    public static IReadOnlyDictionary<NameOrigin, NamePool> CreateDefaultPools()
    {
        var pools = new[]
        {
            Pool(NameOrigin.CanadaEnglish, "Canada",
                ["Noah", "Owen", "Liam", "Mason", "Ethan", "Caleb", "Wyatt", "Carter", "Rylan", "Bennett", "Hudson", "Logan", "Nolan", "Parker", "Connor", "Isaac", "Miles", "Asher", "Dylan", "Brady", "Kieran", "Rowan", "Emmett", "Nathan"],
                ["Clark", "Marlow", "Bishop", "Tanner", "Brooks", "Stone", "Reed", "Morgan", "Fraser", "Hartley", "Mercer", "Shaw", "Foster", "Lane", "Kelly", "Bell", "Price", "Hayes", "Ross", "Ellis", "Grant", "Cross", "Walsh", "Quinn"],
                ["Moose Jaw, SK", "Brandon, MB", "Regina, SK", "Calgary, AB", "Saskatoon, SK", "Kelowna, BC", "Victoria, BC", "Winnipeg, MB", "Medicine Hat, AB", "Red Deer, AB"]),
            Pool(NameOrigin.CanadaFrench, "Canada",
                ["Felix", "Gabriel", "Olivier", "Antoine", "Mathis", "Luca", "Samuel", "Etienne", "Nicolas", "Raphael", "Alexis", "Tristan", "Zacharie", "Loic", "Cedric", "Damien", "Hugo", "Xavier", "Julien", "Marc"],
                ["Lacroix", "Fortin", "Gagnon", "Roy", "Bouchard", "Tremblay", "Bergeron", "Girard", "Pelletier", "Dube", "Moreau", "Beaulieu", "Lemieux", "Caron", "Rousseau", "Desjardins", "Leclerc", "Paquette", "Lavoie", "Perreault"],
                ["Quebec City, QC", "Rimouski, QC", "Gatineau, QC", "Sherbrooke, QC", "Trois-Rivieres, QC", "Chicoutimi, QC"]),
            Pool(NameOrigin.Usa, "USA",
                ["Jack", "Henry", "Lucas", "Cole", "Ryan", "Chase", "Tyler", "Blake", "Evan", "Cooper", "Grayson", "Austin", "Drew", "Gavin", "Luke", "Max", "Cameron", "Wesley", "Colton", "Brody"],
                ["Miller", "Johnson", "Anderson", "Bennett", "Carter", "Hughes", "Parker", "Sullivan", "Walker", "Reynolds", "Baker", "Fletcher", "Harrison", "Coleman", "Warren", "Spencer", "Morrison", "Bates", "Turner", "Foster"],
                ["Minneapolis, MN", "Boston, MA", "Detroit, MI", "Buffalo, NY", "Duluth, MN", "Grand Forks, ND", "Madison, WI"]),
            Pool(NameOrigin.Finland, "Finland",
                ["Aaro", "Eetu", "Mika", "Onni", "Aleksi", "Jere", "Lauri", "Roope", "Teemu", "Kasper", "Niko", "Sami", "Ville", "Oskari", "Antti", "Juuso", "Joni", "Miro", "Patrik", "Tomi"],
                ["Korhonen", "Virtanen", "Makkonen", "Laine", "Heiskanen", "Kapanen", "Nieminen", "Lehtonen", "Rantanen", "Salonen", "Jokinen", "Hakala", "Nurmi", "Leino", "Aaltonen", "Koskinen", "Rinne", "Saarinen", "Pitkanen", "Koivu"],
                ["Helsinki", "Tampere", "Turku", "Oulu", "Espoo", "Lahti"]),
            Pool(NameOrigin.Sweden, "Sweden",
                ["Elias", "Lucas", "Oscar", "Viktor", "Anton", "Linus", "Emil", "Felix", "Axel", "Noel", "Hugo", "Isak", "Nils", "Albin", "Gustav", "Rasmus", "Simon", "Filip", "William", "Theo"],
                ["Johansson", "Andersson", "Karlsson", "Nilsson", "Eriksson", "Larsson", "Soderberg", "Berg", "Lind", "Ekstrom", "Lund", "Nylander", "Backstrom", "Hedman", "Holm", "Forsberg", "Wallin", "Lindholm", "Sundin", "Lindstrom"],
                ["Stockholm", "Gothenburg", "Malmo", "Uppsala", "Vasteras", "Linkoping"]),
            Pool(NameOrigin.Czechia, "Czechia",
                ["Tomas", "Jakub", "Matej", "Pavel", "Roman", "Lukas", "Jan", "Adam", "Martin", "Ondrej", "David", "Filip", "Karel", "Marek", "Radek", "Vojtech", "Patrik", "Daniel", "Michal", "Dominik"],
                ["Novak", "Svoboda", "Dvorak", "Kral", "Havel", "Horak", "Novotny", "Prochazka", "Vesely", "Kucera", "Cerny", "Kadlec", "Pokorny", "Marek", "Blaha", "Sedlak", "Tichy", "Kovar", "Benda", "Jelinek"],
                ["Prague", "Brno", "Ostrava", "Plzen", "Kladno", "Liberec"]),
            Pool(NameOrigin.Slovakia, "Slovakia",
                ["Marek", "Tomas", "Lukas", "Martin", "Patrik", "Samuel", "Adam", "Filip", "Jakub", "David", "Michal", "Peter", "Dominik", "Andrej", "Oliver", "Matej", "Richard", "Branislav", "Erik", "Daniel"],
                ["Horvath", "Kovac", "Novak", "Varga", "Balaz", "Molnar", "Kral", "Hudak", "Mikula", "Urban", "Toth", "Kollar", "Hlinka", "Danko", "Rusnak", "Bartos", "Kovacik", "Mesaros", "Zeman", "Sykora"],
                ["Bratislava", "Kosice", "Nitra", "Zvolen", "Trencin", "Poprad"]),
            Pool(NameOrigin.Russia, "Russia",
                ["Ivan", "Nikita", "Artem", "Dmitri", "Mikhail", "Sergei", "Andrei", "Kirill", "Pavel", "Roman", "Ilya", "Maxim", "Viktor", "Yegor", "Alexei", "Danil", "Timur", "Ruslan", "Vadim", "Oleg"],
                ["Petrov", "Sokolov", "Volkov", "Morozov", "Kuznetsov", "Smirnov", "Orlov", "Fedorov", "Mikhailov", "Romanov", "Nikitin", "Zaitsev", "Belyakov", "Karpov", "Grigorev", "Baranov", "Komarov", "Lebedev", "Voronin", "Tarasov"],
                ["Moscow", "Saint Petersburg", "Yaroslavl", "Kazan", "Omsk", "Novosibirsk"]),
            Pool(NameOrigin.Germany, "Germany",
                ["Lukas", "Leon", "Felix", "Jonas", "Maximilian", "Noah", "Paul", "Finn", "Elias", "Tim", "Moritz", "Julian", "Tobias", "Nico", "Matthias", "Bastian", "Florian", "Dominik", "Simon", "Kai"],
                ["Weber", "Muller", "Schmidt", "Fischer", "Wagner", "Becker", "Hoffmann", "Schneider", "Keller", "Klein", "Wolf", "Hartmann", "Bauer", "Kraus", "Neumann", "Vogel", "Brandt", "Seidel", "Lorenz", "Graf"],
                ["Munich", "Berlin", "Cologne", "Mannheim", "Dusseldorf", "Hamburg"]),
            Pool(NameOrigin.Switzerland, "Switzerland",
                ["Jonas", "Nico", "Luca", "Noah", "Dario", "Simon", "Loris", "Raphael", "Fabian", "Julian", "Marco", "Janis", "Elia", "Matteo", "Sven", "Reto", "Andrin", "Timo", "Kevin", "Pascal"],
                ["Meier", "Meyer", "Muller", "Schmid", "Keller", "Frei", "Weber", "Gerber", "Steiner", "Baumann", "Hug", "Graf", "Huber", "Suter", "Fischer", "Brunner", "Wyss", "Bieri", "Gasser", "Luthi"],
                ["Zurich", "Bern", "Davos", "Lugano", "Lausanne", "Geneva"]),
            Pool(NameOrigin.Latvia, "Latvia",
                ["Janis", "Kristaps", "Arturs", "Rihards", "Roberts", "Martins", "Renars", "Edgars", "Gustavs", "Emils", "Ralfs", "Toms", "Niks", "Davis", "Krisjanis", "Oskars", "Miks", "Andris", "Lauris", "Mareks"],
                ["Berzins", "Ozols", "Kalnins", "Liepa", "Jansons", "Balodis", "Krumins", "Petersons", "Vilks", "Zarins", "Abols", "Eglitis", "Lacis", "Briedis", "Freimanis", "Liepins", "Karklins", "Vitolins", "Rubins", "Sprogis"],
                ["Riga", "Liepaja", "Jelgava", "Daugavpils", "Ventspils", "Valmiera"]),
            Pool(NameOrigin.GenericEuropean, "European Union",
                ["Adrian", "Milan", "Tobias", "Soren", "Henrik", "Matteo", "Nico", "Oscar", "Ronan", "Arlo", "Silas", "Noel", "Kasper", "Emil", "Theo", "Felix", "Roman", "Levi", "Milo", "Ari"],
                ["Moretti", "Conti", "Kovacs", "Havel", "Iversen", "Andersen", "Novotny", "Rinaldi", "Nygaard", "Aasen", "Horak", "Madsen", "Lund", "Berg", "Kral", "Petrov", "Weber", "Johansson", "Svoboda", "Meier"],
                ["Copenhagen", "Oslo", "Milan", "Bolzano", "Budapest", "Aalborg", "Bergen", "Odense", "Prague", "Stockholm"])
        };

        foreach (var pool in pools)
        {
            pool.Validate();
        }

        return pools.ToDictionary(pool => pool.Origin);
    }

    private static NamePool Pool(NameOrigin origin, string nationality, string[] firstNames, string[] lastNames, string[] birthplaces) =>
        new(origin, nationality, firstNames, lastNames, birthplaces);
}
