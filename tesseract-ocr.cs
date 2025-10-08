using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tesseract;

namespace ImageOCR
{
    /// <summary>
    /// Provides stream-based OCR functionality using Tesseract engine with HOCR layout support
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
        /// Gets the number of pages in an image stream (useful for multi-page TIFFs)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>Number of pages in the image</returns>
        public int GetPageCount(Stream imageStream)
        {
            byte[] imageBytes = StreamToByteArray(imageStream);
            
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    return pixArray.Count;
                }
            }
            catch
            {
                // Not a multi-page TIFF or error loading
                return 1;
            }
        }

        /// <summary>
        /// Extracts text from an image stream (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>Extracted text from all pages</returns>
        public string ExtractText(Stream imageStream)
        {
            byte[] imageBytes = StreamToByteArray(imageStream);
            
            // Try to load as multi-page TIFF first
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    if (pixArray.Count > 1)
                    {
                        var textList = new List<string>();
                        foreach (var pix in pixArray)
                        {
                            using (pix)
                            using (var page = _engine.Process(pix))
                            {
                                textList.Add(page.GetText());
                            }
                        }
                        return string.Join("\n\n", textList);
                    }
                }
            }
            catch
            {
                // Not a multi-page TIFF, fall through to single page processing
            }

            // Single page image
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                return page.GetText();
            }
        }

        /// <summary>
        /// Extracts text from a byte array containing image data (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>Extracted text from all pages</returns>
        public string ExtractText(byte[] imageBytes)
        {
            // Try to load as multi-page TIFF first
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    if (pixArray.Count > 1)
                    {
                        var textList = new List<string>();
                        foreach (var pix in pixArray)
                        {
                            using (pix)
                            using (var page = _engine.Process(pix))
                            {
                                textList.Add(page.GetText());
                            }
                        }
                        return string.Join("\n\n", textList);
                    }
                }
            }
            catch
            {
                // Not a multi-page TIFF, fall through to single page processing
            }

            // Single page image
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                return page.GetText();
            }
        }

        /// <summary>
        /// Extracts text with confidence scores (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>Tuple containing the extracted text and average confidence (0-100)</returns>
        public (string Text, float Confidence) ExtractTextWithConfidence(Stream imageStream)
        {
            byte[] imageBytes = StreamToByteArray(imageStream);
            
            // Try to load as multi-page TIFF first
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    if (pixArray.Count > 1)
                    {
                        var textList = new List<string>();
                        var confidenceList = new List<float>();
                        
                        foreach (var pix in pixArray)
                        {
                            using (pix)
                            using (var page = _engine.Process(pix))
                            {
                                textList.Add(page.GetText());
                                confidenceList.Add(page.GetMeanConfidence() * 100);
                            }
                        }
                        
                        string combinedText = string.Join("\n\n", textList);
                        float avgConfidence = confidenceList.Average();
                        return (combinedText, avgConfidence);
                    }
                }
            }
            catch
            {
                // Not a multi-page TIFF, fall through to single page processing
            }

            // Single page image
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                string text = page.GetText();
                float confidence = page.GetMeanConfidence() * 100;
                return (text, confidence);
            }
        }

        /// <summary>
        /// Extracts text with confidence scores from byte array (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>Tuple containing the extracted text and average confidence (0-100)</returns>
        public (string Text, float Confidence) ExtractTextWithConfidence(byte[] imageBytes)
        {
            // Try to load as multi-page TIFF first
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    if (pixArray.Count > 1)
                    {
                        var textList = new List<string>();
                        var confidenceList = new List<float>();
                        
                        foreach (var pix in pixArray)
                        {
                            using (pix)
                            using (var page = _engine.Process(pix))
                            {
                                textList.Add(page.GetText());
                                confidenceList.Add(page.GetMeanConfidence() * 100);
                            }
                        }
                        
                        string combinedText = string.Join("\n\n", textList);
                        float avgConfidence = confidenceList.Average();
                        return (combinedText, avgConfidence);
                    }
                }
            }
            catch
            {
                // Not a multi-page TIFF, fall through to single page processing
            }

            // Single page image
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                string text = page.GetText();
                float confidence = page.GetMeanConfidence() * 100;
                return (text, confidence);
            }
        }

        /// <summary>
        /// Extracts HOCR XHTML layout information from an image stream (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>HOCR XHTML string containing layout information for all pages</returns>
        public string ExtractHocrXhtml(Stream imageStream)
        {
            byte[] imageBytes = StreamToByteArray(imageStream);
            return ExtractHocrXhtml(imageBytes);
        }

        /// <summary>
        /// Extracts HOCR XHTML layout information from a byte array (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>HOCR XHTML string containing layout information for all pages</returns>
        public string ExtractHocrXhtml(byte[] imageBytes)
        {
            var pageHocrList = new List<string>();
            int pageNumber = 0;

            // Try to load as multi-page TIFF first
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    if (pixArray.Count > 1)
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
                        return CombineHocrPagesToXhtml(pageHocrList);
                    }
                }
            }
            catch
            {
                // Not a multi-page TIFF, fall through to single page processing
            }

            // Single page image
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                pageHocrList.Add(page.GetHOCRText(0));
            }

            return CombineHocrPagesToXhtml(pageHocrList);
        }

        /// <summary>
        /// Writes HOCR XHTML to an output stream (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <param name="outputStream">Stream to write the HOCR XHTML to</param>
        public void ExtractHocrXhtmlToStream(Stream imageStream, Stream outputStream)
        {
            string hocr = ExtractHocrXhtml(imageStream);
            using (var writer = new StreamWriter(outputStream, leaveOpen: true))
            {
                writer.Write(hocr);
            }
        }

        /// <summary>
        /// Extracts both text and HOCR XHTML layout information from a stream (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>Tuple containing the plain text and HOCR XHTML for all pages</returns>
        public (string Text, string HocrXhtml) ExtractTextAndHocrXhtml(Stream imageStream)
        {
            byte[] imageBytes = StreamToByteArray(imageStream);
            return ExtractTextAndHocrXhtml(imageBytes);
        }

        /// <summary>
        /// Extracts both text and HOCR XHTML layout information from a byte array (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>Tuple containing the plain text and HOCR XHTML for all pages</returns>
        public (string Text, string HocrXhtml) ExtractTextAndHocrXhtml(byte[] imageBytes)
        {
            var textList = new List<string>();
            var pageHocrList = new List<string>();
            int pageNumber = 0;

            // Try to load as multi-page TIFF first
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    if (pixArray.Count > 1)
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
                        
                        string combinedText = string.Join("\n\n", textList);
                        string hocrXhtml = CombineHocrPagesToXhtml(pageHocrList);
                        return (combinedText, hocrXhtml);
                    }
                }
            }
            catch
            {
                // Not a multi-page TIFF, fall through to single page processing
            }

            // Single page image
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                textList.Add(page.GetText());
                pageHocrList.Add(page.GetHOCRText(0));
            }

            string text = string.Join("\n\n", textList);
            string hocr = CombineHocrPagesToXhtml(pageHocrList);
            return (text, hocr);
        }

        /// <summary>
        /// Extracts text, HOCR XHTML, and confidence from a stream (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>Tuple containing text, HOCR XHTML, and average confidence score for all pages</returns>
        public (string Text, string HocrXhtml, float AverageConfidence) ExtractComplete(Stream imageStream)
        {
            byte[] imageBytes = StreamToByteArray(imageStream);
            return ExtractComplete(imageBytes);
        }

        /// <summary>
        /// Extracts text, HOCR XHTML, and confidence from a byte array (handles all pages in multi-page TIFFs)
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>Tuple containing text, HOCR XHTML, and average confidence score for all pages</returns>
        public (string Text, string HocrXhtml, float AverageConfidence) ExtractComplete(byte[] imageBytes)
        {
            var textList = new List<string>();
            var pageHocrList = new List<string>();
            var confidenceList = new List<float>();
            int pageNumber = 0;

            // Try to load as multi-page TIFF first
            try
            {
                using (var pixArray = PixArray.LoadMultiPageTiffFromMemory(imageBytes))
                {
                    if (pixArray.Count > 1)
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
                        
                        string combinedText = string.Join("\n\n", textList);
                        string hocrXhtml = CombineHocrPagesToXhtml(pageHocrList);
                        float avgConfidence = confidenceList.Average();
                        return (combinedText, hocrXhtml, avgConfidence);
                    }
                }
            }
            catch
            {
                // Not a multi-page TIFF, fall through to single page processing
            }

            // Single page image
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = _engine.Process(img))
            {
                textList.Add(page.GetText());
                pageHocrList.Add(page.GetHOCRText(0));
                confidenceList.Add(page.GetMeanConfidence() * 100);
            }

            string text = string.Join("\n\n", textList);
            string hocr = CombineHocrPagesToXhtml(pageHocrList);
            float confidence = confidenceList.Average();
            return (text, hocr, confidence);
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
