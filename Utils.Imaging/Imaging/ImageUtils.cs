using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing.Imaging;

namespace Utils.Imaging
{
	public static class ImageUtils
	{
        private static readonly Dictionary<string, ImageCodecInfo> ImageEncoderInfos = new Dictionary<string, ImageCodecInfo>();
        private static readonly Dictionary<string, ImageCodecInfo> ImageDecoderInfos = new Dictionary<string, ImageCodecInfo>();

        public static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            if (ImageEncoderInfos.TryGetValue(mimeType, out ImageCodecInfo encoder)) return encoder;

            int j;
            ImageCodecInfo result = null;
            var encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                encoder = encoders[j];
                ImageEncoderInfos[encoder.MimeType] = encoder;
                if (encoder.MimeType == mimeType)
                {
                    result = encoder;
                }
            }
            return result;
        }

        public static ImageCodecInfo GetDecoderInfo(string mimeType)
        {
            if (ImageDecoderInfos.TryGetValue(mimeType, out ImageCodecInfo encoder)) return encoder;

            int j;
            ImageCodecInfo result = null;
            var encoders = ImageCodecInfo.GetImageDecoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                encoder = encoders[j];
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
