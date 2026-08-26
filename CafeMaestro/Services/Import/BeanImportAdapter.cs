using CafeMaestro.Models;
using CafeMaestro.Navigation;

namespace CafeMaestro.Services;

/// <summary>
/// Bean inventory import: field definitions, row validation, duplicate policy, and the append
/// that runs inside the atomic commit.
/// </summary>
public sealed class BeanImportAdapter : IImportAdapter
{
    /// <summary>Legacy default so an unpriced, unmeasured row still yields usable inventory.</summary>
    internal const double DefaultQuantityKg = 1;

    private readonly IAppDataService _appDataService;

    public BeanImportAdapter(IAppDataService appDataService)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
    }

    public ImportKindDescriptor Descriptor { get; } = new(
        ImportKind.Beans,
        "Coffee beans",
        "Inventory, origin, variety, process, quantity",
        "bean",
        "beans",
        "Select CSV file with bean data",
        "VIEW BEANS",
        Routes.BeanInventory);

    public IReadOnlyList<ImportFieldDefinition> Fields { get; } =
    [
        new("CoffeeName", "Coffee name", true, ["coffee", "name", "bean"], ExactAliases: ["coffee"]),
        new("Country", "Country", true, ["country", "origin", "region"]),
        new("Variety", "Variety", false, ["variety", "varietal", "cultivar"], ExactAliases: ["variaty"]),
        new("Process", "Process", false, ["process", "method", "processing"]),
        new("PurchaseDate", "Purchase date", false, ["date", "purchase", "acquired"]),
        new("Quantity", "Quantity (kg)", false, ["quantity", "amount", "weight", "kg", "order"], ContainsAliases: ["oreder"]),
        new("Price", "Price", false, ["price", "cost"]),
        new("Notes", "Notes", false, ["note", "notes", "description", "flavor", "profile"]),
        new("Link", "Link", false, ["link", "url", "website", "web"])
    ];

    public async Task<IImportSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AppData snapshot = await _appDataService.LoadAppDataAsync();
        return new BeanImportSession(snapshot.Beans ?? []);
    }

    private sealed class BeanImportSession : IImportSession
    {
        private readonly List<BeanData> _accepted = [];
        private readonly HashSet<string> _signatures;

        public BeanImportSession(IEnumerable<BeanData> existingBeans)
        {
            _signatures = new HashSet<string>(
                existingBeans.Select(CreateSignature),
                StringComparer.OrdinalIgnoreCase);
        }

        public int AcceptedCount => _accepted.Count;

        public ImportRowOutcome Evaluate(
            int rowNumber,
            IReadOnlyDictionary<string, string> row,
            IReadOnlyDictionary<string, string> mappings)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(mappings);

            string coffeeName = ImportHeaderMatcher.GetMappedValue(row, mappings, "CoffeeName");
            string country = ImportHeaderMatcher.GetMappedValue(row, mappings, "Country");

            if (string.IsNullOrWhiteSpace(coffeeName))
            {
                return Reject(rowNumber, coffeeName, country, "Coffee name is required.");
            }

            if (string.IsNullOrWhiteSpace(country))
            {
                return Reject(rowNumber, coffeeName, country, "Country is required.");
            }

            var bean = new BeanData
            {
                Id = Guid.NewGuid(),
                CoffeeName = coffeeName,
                Country = country,
                Variety = ImportHeaderMatcher.GetMappedValue(row, mappings, "Variety"),
                Process = ImportHeaderMatcher.GetMappedValue(row, mappings, "Process"),
                Notes = ImportHeaderMatcher.GetMappedValue(row, mappings, "Notes"),
                Link = ImportHeaderMatcher.GetMappedValue(row, mappings, "Link"),
                PurchaseDate = DateTime.Now
            };

            string purchaseDate = ImportHeaderMatcher.GetMappedValue(row, mappings, "PurchaseDate");

            if (!string.IsNullOrWhiteSpace(purchaseDate))
            {
                if (!ImportValueParser.TryParseDate(purchaseDate, out DateTime parsedDate))
                {
                    return Reject(rowNumber, coffeeName, country, $"Purchase date '{purchaseDate}' is not a recognised date.");
                }

                bean.PurchaseDate = parsedDate;
            }

            string quantity = ImportHeaderMatcher.GetMappedValue(row, mappings, "Quantity");

            if (!string.IsNullOrWhiteSpace(quantity))
            {
                if (!ImportValueParser.TryParseNumber(quantity, out double parsedQuantity))
                {
                    return Reject(rowNumber, coffeeName, country, $"Quantity '{quantity}' is not a number.");
                }

                if (parsedQuantity < 0)
                {
                    return Reject(rowNumber, coffeeName, country, "Quantity must be zero or greater.");
                }

                bean.Quantity = parsedQuantity;
            }

            if (bean.Quantity <= 0)
            {
                bean.Quantity = DefaultQuantityKg;
            }

            bean.RemainingQuantity = bean.Quantity;

            string price = ImportHeaderMatcher.GetMappedValue(row, mappings, "Price");

            if (!string.IsNullOrWhiteSpace(price))
            {
                if (!ImportValueParser.TryParsePrice(price, out decimal parsedPrice))
                {
                    return Reject(rowNumber, coffeeName, country, $"Price '{price}' is not a number.");
                }

                bean.Price = parsedPrice;
            }

            List<string> validationErrors = bean.Validate();

            if (validationErrors.Count > 0)
            {
                return Reject(rowNumber, coffeeName, country, validationErrors[0]);
            }

            string signature = CreateSignature(bean);

            if (!_signatures.Add(signature))
            {
                return Reject(
                    rowNumber,
                    coffeeName,
                    country,
                    $"'{coffeeName}' from {country} is already in the inventory.");
            }

            _accepted.Add(bean);

            var details = new List<string>();

            if (!string.IsNullOrWhiteSpace(bean.Process))
            {
                details.Add(bean.Process);
            }

            if (!string.IsNullOrWhiteSpace(bean.Variety))
            {
                details.Add(bean.Variety);
            }

            details.Add(bean.TotalQuantityDisplay);

            return new ImportRowOutcome(
                rowNumber,
                true,
                $"{coffeeName}",
                string.Join(" · ", details.Prepend(country)));
        }

        public void Commit(AppData appData)
        {
            ArgumentNullException.ThrowIfNull(appData);
            appData.Beans ??= [];
            appData.Beans.AddRange(_accepted);
        }

        private static ImportRowOutcome Reject(int rowNumber, string coffeeName, string country, string error)
        {
            string label = string.IsNullOrWhiteSpace(coffeeName)
                ? string.IsNullOrWhiteSpace(country) ? "no identifying values" : country
                : coffeeName;

            return new ImportRowOutcome(rowNumber, false, $"Row {rowNumber} · {label}", error);
        }

        /// <summary>
        /// Duplicate policy: the same coffee name, country, and variety already in inventory.
        /// </summary>
        private static string CreateSignature(BeanData bean) =>
            $"{bean.CoffeeName?.Trim()}|{bean.Country?.Trim()}|{bean.Variety?.Trim()}";
    }
}
