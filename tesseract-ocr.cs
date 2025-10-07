using System;
using System.IO;
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
        /// Extracts text from an image file
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Extracted text from the image</returns>
        public string ExtractTextFromImage(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            using (var img = Pix.LoadFromFile(imagePath))
            {
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
            {
                using (var page = _engine.Process(img))
                {
                    return page.GetText();
                }
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
            {
                using (var page = _engine.Process(img))
                {
                    return page.GetText();
                }
            }
        }

        /// <summary>
        /// Extracts text with confidence scores
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Tuple containing the extracted text and mean confidence (0-100)</returns>
        public (string Text, float Confidence) ExtractTextWithConfidence(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            using (var img = Pix.LoadFromFile(imagePath))
            {
                using (var page = _engine.Process(img))
                {
                    string text = page.GetText();
                    float confidence = page.GetMeanConfidence() * 100;
                    return (text, confidence);
                }
            }
        }

        /// <summary>
        /// Extracts HOCR layout information from an image file and saves it
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="hocrOutputPath">Path where the HOCR file will be saved</param>
        public void ExtractAndSaveHocr(string imagePath, string hocrOutputPath)
        {
            string hocr = ExtractHocr(imagePath);
            File.WriteAllText(hocrOutputPath, hocr);
        }

        /// <summary>
        /// Extracts HOCR layout information from an image file
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>HOCR HTML string containing layout information</returns>
        public string ExtractHocr(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            using (var img = Pix.LoadFromFile(imagePath))
            {
                using (var page = _engine.Process(img))
                {
                    return page.GetHOCRText(0);
                }
            }
        }

        /// <summary>
        /// Extracts HOCR layout information from an image stream
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <returns>HOCR HTML string containing layout information</returns>
        public string ExtractHocrFromStream(Stream imageStream)
        {
            using (var img = Pix.LoadFromMemory(StreamToByteArray(imageStream)))
            {
                using (var page = _engine.Process(img))
                {
                    return page.GetHOCRText(0);
                }
            }
        }

        /// <summary>
        /// Extracts HOCR layout information from a byte array
        /// </summary>
        /// <param name="imageBytes">Byte array of the image</param>
        /// <returns>HOCR HTML string containing layout information</returns>
        public string ExtractHocrFromBytes(byte[] imageBytes)
        {
            using (var img = Pix.LoadFromMemory(imageBytes))
            {
                using (var page = _engine.Process(img))
                {
                    return page.GetHOCRText(0);
                }
            }
        }

        /// <summary>
        /// Extracts both text and HOCR layout information from an image
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Tuple containing the plain text and HOCR HTML</returns>
        public (string Text, string Hocr) ExtractTextAndHocr(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            using (var img = Pix.LoadFromFile(imagePath))
            {
                using (var page = _engine.Process(img))
                {
                    string text = page.GetText();
                    string hocr = page.GetHOCRText(0);
                    return (text, hocr);
                }
            }
        }

        /// <summary>
        /// Extracts text, HOCR, and confidence from an image
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Tuple containing text, HOCR HTML, and confidence score</returns>
        public (string Text, string Hocr, float Confidence) ExtractComplete(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            using (var img = Pix.LoadFromFile(imagePath))
            {
                using (var page = _engine.Process(img))
                {
                    string text = page.GetText();
                    string hocr = page.GetHOCRText(0);
                    float confidence = page.GetMeanConfidence() * 100;
                    return (text, hocr, confidence);
                }
            }
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
