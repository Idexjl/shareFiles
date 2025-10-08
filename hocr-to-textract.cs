using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace ImageOCR
{
    /// <summary>
    /// Converts HOCR XHTML format to Amazon Textract JSON format
    /// </summary>
    public class HocrToTextractConverter
    {
        /// <summary>
        /// Converts HOCR XHTML string to Amazon Textract JSON format
        /// </summary>
        /// <param name="hocrXhtml">HOCR XHTML string</param>
        /// <returns>Textract-formatted JSON string</returns>
        public string ConvertToTextract(string hocrXhtml)
        {
            var doc = XDocument.Parse(hocrXhtml);
            var blocks = new List<TextractBlock>();
            var blockIdCounter = 1;

            // Find all pages
            var pages = doc.Descendants()
                .Where(e => e.Attribute("class")?.Value == "ocr_page")
                .ToList();

            foreach (var page in pages)
            {
                var pageInfo = ParseTitle(page.Attribute("title")?.Value ?? "");
                int pageNumber = pages.IndexOf(page) + 1;

                // Create PAGE block
                var pageBlock = new TextractBlock
                {
                    BlockType = "PAGE",
                    Id = $"page-{pageNumber}",
                    Page = pageNumber,
                    Geometry = CreateGeometry(pageInfo),
                    Relationships = new List<Relationship>
                    {
                        new Relationship { Type = "CHILD", Ids = new List<string>() }
                    }
                };
                blocks.Add(pageBlock);

                // Find all lines in this page
                var lines = page.Descendants()
                    .Where(e => e.Attribute("class")?.Value == "ocr_line")
                    .ToList();

                foreach (var line in lines)
                {
                    var lineInfo = ParseTitle(line.Attribute("title")?.Value ?? "");
                    var lineId = $"line-{blockIdCounter++}";
                    
                    // Create LINE block
                    var lineBlock = new TextractBlock
                    {
                        BlockType = "LINE",
                        Id = lineId,
                        Page = pageNumber,
                        Text = GetElementText(line),
                        Geometry = CreateGeometry(lineInfo),
                        Relationships = new List<Relationship>
                        {
                            new Relationship { Type = "CHILD", Ids = new List<string>() }
                        }
                    };

                    // Add line to page's children
                    pageBlock.Relationships[0].Ids.Add(lineId);

                    // Find all words in this line
                    var words = line.Descendants()
                        .Where(e => e.Attribute("class")?.Value == "ocrx_word")
                        .ToList();

                    foreach (var word in words)
                    {
                        var wordInfo = ParseTitle(word.Attribute("title")?.Value ?? "");
                        var wordId = $"word-{blockIdCounter++}";
                        var wordText = GetElementText(word);

                        // Extract confidence if available
                        float confidence = 0;
                        if (wordInfo.ContainsKey("x_wconf"))
                        {
                            float.TryParse(wordInfo["x_wconf"], out confidence);
                        }

                        // Create WORD block
                        var wordBlock = new TextractBlock
                        {
                            BlockType = "WORD",
                            Id = wordId,
                            Page = pageNumber,
                            Text = wordText,
                            Confidence = confidence,
                            Geometry = CreateGeometry(wordInfo)
                        };

                        // Add word to line's children
                        lineBlock.Relationships[0].Ids.Add(wordId);
                        blocks.Add(wordBlock);
                    }

                    blocks.Add(lineBlock);
                }
            }

            // Create Textract response structure
            var textractResponse = new TextractResponse
            {
                DocumentMetadata = new DocumentMetadata
                {
                    Pages = pages.Count
                },
                Blocks = blocks
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(textractResponse, options);
        }

        /// <summary>
        /// Parses the HOCR title attribute into a dictionary
        /// </summary>
        private Dictionary<string, string> ParseTitle(string title)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(title))
                return result;

            var parts = title.Split(';');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var spaceIndex = trimmed.IndexOf(' ');
                if (spaceIndex > 0)
                {
                    var key = trimmed.Substring(0, spaceIndex);
                    var value = trimmed.Substring(spaceIndex + 1);
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Creates Textract geometry from HOCR bbox information
        /// </summary>
        private Geometry CreateGeometry(Dictionary<string, string> info)
        {
            if (!info.ContainsKey("bbox"))
                return null;

            var bbox = info["bbox"].Split(' ');
            if (bbox.Length != 4)
                return null;

            float.TryParse(bbox[0], out float left);
            float.TryParse(bbox[1], out float top);
            float.TryParse(bbox[2], out float right);
            float.TryParse(bbox[3], out float bottom);

            // Get page dimensions if available
            float pageWidth = 1000;
            float pageHeight = 1000;
            
            if (info.ContainsKey("ppageno"))
            {
                // Try to get actual page dimensions from image info
                // For now, use defaults or you can pass these in
            }

            var width = right - left;
            var height = bottom - top;

            return new Geometry
            {
                BoundingBox = new BoundingBox
                {
                    Width = width / pageWidth,
                    Height = height / pageHeight,
                    Left = left / pageWidth,
                    Top = top / pageHeight
                },
                Polygon = new List<Point>
                {
                    new Point { X = left / pageWidth, Y = top / pageHeight },
                    new Point { X = right / pageWidth, Y = top / pageHeight },
                    new Point { X = right / pageWidth, Y = bottom / pageHeight },
                    new Point { X = left / pageWidth, Y = bottom / pageHeight }
                }
            };
        }

        /// <summary>
        /// Gets the text content from an XML element
        /// </summary>
        private string GetElementText(XElement element)
        {
            return string.Join(" ", element.Descendants()
                .Where(e => !e.HasElements)
                .Select(e => e.Value.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v)));
        }
    }

    #region Textract Data Models

    public class TextractResponse
    {
        [JsonPropertyName("DocumentMetadata")]
        public DocumentMetadata DocumentMetadata { get; set; }

        [JsonPropertyName("Blocks")]
        public List<TextractBlock> Blocks { get; set; }
    }

    public class DocumentMetadata
    {
        [JsonPropertyName("Pages")]
        public int Pages { get; set; }
    }

    public class TextractBlock
    {
        [JsonPropertyName("BlockType")]
        public string BlockType { get; set; }

        [JsonPropertyName("Id")]
        public string Id { get; set; }

        [JsonPropertyName("Page")]
        public int Page { get; set; }

        [JsonPropertyName("Text")]
        public string Text { get; set; }

        [JsonPropertyName("Confidence")]
        public float? Confidence { get; set; }

        [JsonPropertyName("Geometry")]
        public Geometry Geometry { get; set; }

        [JsonPropertyName("Relationships")]
        public List<Relationship> Relationships { get; set; }
    }

    public class Relationship
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; }

        [JsonPropertyName("Ids")]
        public List<string> Ids { get; set; }
    }

    public class Geometry
    {
        [JsonPropertyName("BoundingBox")]
        public BoundingBox BoundingBox { get; set; }

        [JsonPropertyName("Polygon")]
        public List<Point> Polygon { get; set; }
    }

    public class BoundingBox
    {
        [JsonPropertyName("Width")]
        public float Width { get; set; }

        [JsonPropertyName("Height")]
        public float Height { get; set; }

        [JsonPropertyName("Left")]
        public float Left { get; set; }

        [JsonPropertyName("Top")]
        public float Top { get; set; }
    }

    public class Point
    {
        [JsonPropertyName("X")]
        public float X { get; set; }

        [JsonPropertyName("Y")]
        public float Y { get; set; }
    }

    #endregion
}
