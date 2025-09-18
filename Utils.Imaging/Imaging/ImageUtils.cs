using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing.Imaging;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides helpers to resolve image encoders and decoders by MIME type.
    /// </summary>
    public static class ImageUtils
    {
        private static readonly Dictionary<string, ImageCodecInfo> ImageEncoderInfos = new();
        private static readonly Dictionary<string, ImageCodecInfo> ImageDecoderInfos = new();

        /// <summary>
        /// Retrieves the <see cref="ImageCodecInfo"/> encoder matching the provided MIME type.
        /// </summary>
        /// <param name="mimeType">Encoder MIME type to search.</param>
        /// <returns>The matching encoder, or <see langword="null"/> when none exists.</returns>
        public static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            if (ImageEncoderInfos.TryGetValue(mimeType, out ImageCodecInfo encoder))
            {
                return encoder;
            }

            ImageCodecInfo result = null;
            var encoders = ImageCodecInfo.GetImageEncoders();
            for (var index = 0; index < encoders.Length; ++index)
            {
                encoder = encoders[index];
                ImageEncoderInfos[encoder.MimeType] = encoder;
                if (encoder.MimeType == mimeType)
                {
                    result = encoder;
                }
            }

            return result;
        }

        /// <summary>
        /// Retrieves the <see cref="ImageCodecInfo"/> decoder matching the provided MIME type.
        /// </summary>
        /// <param name="mimeType">Decoder MIME type to search.</param>
        /// <returns>The matching decoder, or <see langword="null"/> when none exists.</returns>
        public static ImageCodecInfo GetDecoderInfo(string mimeType)
        {
            if (ImageDecoderInfos.TryGetValue(mimeType, out ImageCodecInfo encoder))
            {
                return encoder;
            }

            ImageCodecInfo result = null;
            var decoders = ImageCodecInfo.GetImageDecoders();
            for (var index = 0; index < decoders.Length; ++index)
            {
                encoder = decoders[index];
                ImageDecoderInfos[encoder.MimeType] = encoder;
                if (encoder.MimeType == mimeType)
                {
                    result = encoder;
                }
            }

            return result;
        }
    }
}
