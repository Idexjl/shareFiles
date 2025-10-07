using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tesseract;

namespace ImageOCR
{
    /// <summary>
    /// Provides OCR functionality using Tesseract engine with HOCR layout support
    /// </summary>
    public class TesseractOcrService : IDisposable
    {
        private readonly TesseractEngine _engine;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the TesseractOcrService
        /// </summary>
        /// <param name="tessDataPath">Path to the tessdata folder containing language data files</param>
        /// <param name="language">Language code (default: "eng" for English)</param>
        public TesseractOcrService(string tessDataPath = "./tessdata", string language = "eng")
        {
            if (!Directory.Exists(tessDataPath))
            {
                throw new DirectoryNotFoundException($"Tessdata directory not found at: {tessDataPath}");
            }

            _engine = new TesseractEngine(tessDataPath, language, EngineMode.Default);
        }

        /// <summary>
        /// Gets the number of pages in an image file (useful for multi-page TIFFs)
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Number of pages in the image</returns>
        public int GetPageCount(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            using (var pixArray = PixArray.LoadMultiPageTiffFromFile(imagePath))
            {
                return pixArray.Count;
            }
        }

        /// <summary>
        /// Extracts text from an image file (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Extracted text from all pages</returns>
        public string ExtractTextFromImage(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            // Check if it's a multi-page TIFF
            if (IsMultiPageTiff(imagePath))
            {
                var textList = new List<string>();
                using (var pixArray = PixArray.LoadMultiPageTiffFromFile(imagePath))
                {
                    foreach (var pix in pixArray)
                    {
                        using (pix)
                        using (var page = _engine.Process(pix))
                        {
                            textList.Add(page.GetText());
                        }
                    }
                }
                return string.Join("\n\n", textList);
            }
            else
            {
                using (var img = Pix.LoadFromFile(imagePath))
                using (var page = _engine.Process(img))
                {
                    return page.GetText();
                }
            }
        }

        /// <summary>
        /// Extracts text from an image stream
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>Extracted text from the image</returns>
        public string ExtractTextFromStream(Stream imageStream)
        {
            using (var img = Pix.LoadFromMemory(StreamToByteArray(imageStream)))
            using (var page = _engine.Process(img))
            {
                return page.GetText();
            }
        }

        /// <summary>
        /// Extracts text from a byte array containing image data
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>Extracted text from the image</returns>
        public string ExtractTextFromBytes(byte[] imageBytes)
        {
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                return page.GetText();
            }
        }

        /// <summary>
        /// Extracts text with confidence scores (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Tuple containing the extracted text and average confidence (0-100)</returns>
        public (string Text, float Confidence) ExtractTextWithConfidence(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            if (IsMultiPageTiff(imagePath))
            {
                var textList = new List<string>();
                var confidenceList = new List<float>();
                
                using (var pixArray = PixArray.LoadMultiPageTiffFromFile(imagePath))
                {
                    foreach (var pix in pixArray)
                    {
                        using (pix)
                        using (var page = _engine.Process(pix))
                        {
                            textList.Add(page.GetText());
                            confidenceList.Add(page.GetMeanConfidence() * 100);
                        }
                    }
                }
                
                string combinedText = string.Join("\n\n", textList);
                float avgConfidence = confidenceList.Average();
                return (combinedText, avgConfidence);
            }
            else
            {
                using (var img = Pix.LoadFromFile(imagePath))
                using (var page = _engine.Process(img))
                {
                    string text = page.GetText();
                    float confidence = page.GetMeanConfidence() * 100;
                    return (text, confidence);
                }
            }
        }

        /// <summary>
        /// Extracts HOCR XHTML layout information from an image file (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="hocrOutputPath">Path where the HOCR XHTML file will be saved</param>
        public void ExtractAndSaveHocrXhtml(string imagePath, string hocrOutputPath)
        {
            string hocr = ExtractHocrXhtml(imagePath);
            File.WriteAllText(hocrOutputPath, hocr);
        }

        /// <summary>
        /// Extracts HOCR XHTML layout information from an image file (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>HOCR XHTML string containing layout information for all pages</returns>
        public string ExtractHocrXhtml(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            var pageHocrList = new List<string>();
            int pageNumber = 0;

            if (IsMultiPageTiff(imagePath))
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromFile(imagePath))
                {
                    foreach (var pix in pixArray)
                    {
                        using (pix)
                        using (var page = _engine.Process(pix))
                        {
                            pageHocrList.Add(page.GetHOCRText(pageNumber));
                            pageNumber++;
                        }
                    }
                }
            }
            else
            {
                using (var img = Pix.LoadFromFile(imagePath))
                using (var page = _engine.Process(img))
                {
                    pageHocrList.Add(page.GetHOCRText(0));
                }
            }

            return CombineHocrPagesToXhtml(pageHocrList);
        }

        /// <summary>
        /// Extracts HOCR layout information from an image stream
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>HOCR XHTML string containing layout information</returns>
        public string ExtractHocrXhtmlFromStream(Stream imageStream)
        {
            using (var img = Pix.LoadFromMemory(StreamToByteArray(imageStream)))
            using (var page = _engine.Process(img))
            {
                var pageHocrList = new List<string> { page.GetHOCRText(0) };
                return CombineHocrPagesToXhtml(pageHocrList);
            }
        }

        /// <summary>
        /// Extracts HOCR layout information from a byte array
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>HOCR XHTML string containing layout information</returns>
        public string ExtractHocrXhtmlFromBytes(byte[] imageBytes)
        {
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                var pageHocrList = new List<string> { page.GetHOCRText(0) };
                return CombineHocrPagesToXhtml(pageHocrList);
            }
        }

        /// <summary>
        /// Extracts both text and HOCR XHTML layout information from an image (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Tuple containing the plain text and HOCR XHTML for all pages</returns>
        public (string Text, string HocrXhtml) ExtractTextAndHocrXhtml(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            var textList = new List<string>();
            var pageHocrList = new List<string>();
            int pageNumber = 0;

            if (IsMultiPageTiff(imagePath))
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromFile(imagePath))
                {
                    foreach (var pix in pixArray)
                    {
                        using (pix)
                        using (var page = _engine.Process(pix))
                        {
                            textList.Add(page.GetText());
                            pageHocrList.Add(page.GetHOCRText(pageNumber));
                            pageNumber++;
                        }
                    }
                }
            }
            else
            {
                using (var img = Pix.LoadFromFile(imagePath))
                using (var page = _engine.Process(img))
                {
                    textList.Add(page.GetText());
                    pageHocrList.Add(page.GetHOCRText(0));
                }
            }

            string combinedText = string.Join("\n\n", textList);
            string hocrXhtml = CombineHocrPagesToXhtml(pageHocrList);
            
            return (combinedText, hocrXhtml);
        }

        /// <summary>
        /// Extracts text, HOCR XHTML, and confidence from an image (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Tuple containing text, HOCR XHTML, and average confidence score for all pages</returns>
        public (string Text, string HocrXhtml, float AverageConfidence) ExtractCompleteXhtml(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            var textList = new List<string>();
            var pageHocrList = new List<string>();
            var confidenceList = new List<float>();
            int pageNumber = 0;

            if (IsMultiPageTiff(imagePath))
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromFile(imagePath))
                {
                    foreach (var pix in pixArray)
                    {
                        using (pix)
                        using (var page = _engine.Process(pix))
                        {
                            textList.Add(page.GetText());
                            pageHocrList.Add(page.GetHOCRText(pageNumber));
                            confidenceList.Add(page.GetMeanConfidence() * 100);
                            pageNumber++;
                        }
                    }
                }
            }
            else
            {
                using (var img = Pix.LoadFromFile(imagePath))
                using (var page = _engine.Process(img))
                {
                    textList.Add(page.GetText());
                    pageHocrList.Add(page.GetHOCRText(0));
                    confidenceList.Add(page.GetMeanConfidence() * 100);
                }
            }

            string combinedText = string.Join("\n\n", textList);
            string hocrXhtml = CombineHocrPagesToXhtml(pageHocrList);
            float avgConfidence = confidenceList.Average();
            
            return (combinedText, hocrXhtml, avgConfidence);
        }

        /// <summary>
        /// Checks if an image file is a multi-page TIFF
        /// </summary>
        private bool IsMultiPageTiff(string imagePath)
        {
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromFile(imagePath))
                {
                    return pixArray.Count > 1;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Combines multiple HOCR page fragments into a complete XHTML document
        /// </summary>
        private string CombineHocrPagesToXhtml(List<string> pageHocrFragments)
        {
            var xhtmlHeader = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" xml:lang=""en"" lang=""en"">
<head>
<title>OCR Results</title>
<meta http-equiv=""content-type"" content=""text/html; charset=utf-8"" />
<meta name=""ocr-system"" content=""tesseract"" />
<meta name=""ocr-capabilities"" content=""ocr_page ocr_carea ocr_par ocr_line ocrx_word"" />
</head>
<body>";

            var xhtmlFooter = @"
</body>
</html>";

            var bodyContent = new System.Text.StringBuilder();
            
            foreach (var pageHocr in pageHocrFragments)
            {
                // Extract the body content from each page's HOCR
                var bodyStartIndex = pageHocr.IndexOf("<body>");
                var bodyEndIndex = pageHocr.IndexOf("</body>");
                
                if (bodyStartIndex >= 0 && bodyEndIndex >= 0)
                {
                    bodyStartIndex += "<body>".Length;
                    var bodyFragment = pageHocr.Substring(bodyStartIndex, bodyEndIndex - bodyStartIndex);
                    bodyContent.AppendLine(bodyFragment);
                }
            }

            return xhtmlHeader + bodyContent.ToString() + xhtmlFooter;
        }

        /// <summary>
        /// Sets a Tesseract variable for fine-tuning OCR behavior
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <param name="value">Variable value</param>
        public void SetVariable(string name, string value)
        {
            _engine.SetVariable(name, value);
        }

        private byte[] StreamToByteArray(Stream stream)
        {
            if (stream is MemoryStream memoryStream)
            {
                return memoryStream.ToArray();
            }

            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _engine?.Dispose();
                }
                _disposed = true;
            }
        }

        ~TesseractOcrService()
        {
            Dispose(false);
        }
    }
}
