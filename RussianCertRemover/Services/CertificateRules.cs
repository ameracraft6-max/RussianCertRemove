using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using RussianCertRemover.Models;

namespace RussianCertRemover.Services
{
    public static class CertificateRules
    {
        // Known Russian Trusted Root / Sub CA SHA-1 Thumbprints (uppercase without spaces)
        public static readonly HashSet<string> KnownRussianThumbprints = new(StringComparer.OrdinalIgnoreCase)
        {
            "8FF915CCAB7BC16F8C5C8099D53E0E115B3AEC2F", // Russian Trusted Root CA (Primary)
            "EE4721C2B0DF7A3485E4BF59CF46F355C667469B", // Russian Trusted Root CA 2022
            "7D3EFB185A48DEEE63510A2B93D0F70EB3C4E3D8", // Russian Trusted Sub CA
            "E232F9C7D52F7FE621F06ED0FDBC6778D46A942E", // Russian Trusted Sub CA alternative
            "98FE143AE4CA9E72FE989803EB4FF1EFC9724185", // Минцифры тестовые / доп. центры
            "74BFB47291B7F7D25DF3EC69D6864E595D31D4D7"
        };

        // Textual markers in Subject, Issuer, or FriendlyName
        private static readonly string[] RussianCaMarkers =
        {
            "Russian Trusted Root CA",
            "Russian Trusted Sub CA",
            "Russian Trusted",
            "Минцифры",
            "Министерство цифрового развития",
            "Госуслуги",
            "Gosuslugi",
            "Национальный удостоверяющий центр",
            "НАЦИОНАЛЬНЫЙ УДОСТОВЕРЯЮЩИЙ ЦЕНТР",
            "National Certification Authority",
            "Ministry of Digital Development",
            "АО «ГНИВЦ»",
            "АО \"ГНИВЦ\"",
            "ГНИВЦ",
            "НТЦ ИТ РОСА"
        };

        public static bool IsRussianMintsifryCert(X509Certificate2 cert, out string reason, out CertCategory category)
        {
            reason = string.Empty;
            category = CertCategory.Other;

            string thumbprint = cert.Thumbprint?.Replace(" ", "").Trim() ?? "";

            // Check known thumbprints
            if (KnownRussianThumbprints.Contains(thumbprint))
            {
                reason = "Совпадение по известному SHA-1 отпечатку НУЦ Минцифры";
                category = cert.Subject.Contains("Sub", StringComparison.OrdinalIgnoreCase)
                    ? CertCategory.SubCA
                    : CertCategory.RootCA;
                return true;
            }

            string subject = cert.Subject ?? "";
            string issuer = cert.Issuer ?? "";
            string friendlyName = cert.FriendlyName ?? "";

            // Check markers in Subject
            foreach (var marker in RussianCaMarkers)
            {
                if (subject.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                    friendlyName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"Обнаружен маркер '{marker}' в данных субъекта сертификата";
                    category = DetermineCategory(subject, issuer);
                    return true;
                }
            }

            // Check markers in Issuer
            foreach (var marker in RussianCaMarkers)
            {
                if (issuer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"Обнаружен маркер '{marker}' в данных издателя (Issuer)";
                    category = DetermineCategory(subject, issuer);
                    return true;
                }
            }

            return false;
        }

        private static CertCategory DetermineCategory(string subject, string issuer)
        {
            if (subject.Contains("Root CA", StringComparison.OrdinalIgnoreCase) ||
                subject.Contains("Корневой", StringComparison.OrdinalIgnoreCase))
            {
                return CertCategory.RootCA;
            }

            if (subject.Contains("Sub CA", StringComparison.OrdinalIgnoreCase) ||
                subject.Contains("Промежуточный", StringComparison.OrdinalIgnoreCase) ||
                subject.Contains("Подчиненный", StringComparison.OrdinalIgnoreCase))
            {
                return CertCategory.SubCA;
            }

            if (subject.Equals(issuer, StringComparison.OrdinalIgnoreCase))
            {
                return CertCategory.RootCA;
            }

            return CertCategory.SubCA;
        }

        public static string ExtractCommonName(string distinguishedName)
        {
            if (string.IsNullOrWhiteSpace(distinguishedName)) return "Без имени";
            var parts = distinguishedName.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            var cn = parts.FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase));
            if (cn != null && cn.Length > 3)
            {
                return cn.Substring(3).Trim('"');
            }
            return distinguishedName;
        }
    }
}
