using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS.RFC1876
{
    [DNSClass(0x1D)]
    public class LOC : DNSResponseDetail
    {
        /*
       MSB                                           LSB
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
      0|        VERSION        |         SIZE          |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
      2|       HORIZ PRE       |       VERT PRE        |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
      4|                   LATITUDE                    |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
      6|                   LATITUDE                    |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
      8|                   LONGITUDE                   |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
     10|                   LONGITUDE                   |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
     12|                   ALTITUDE                    |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
     14|                   ALTITUDE                    |
       +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
   (octet)

where:

VERSION      Version number of the representation.  This must be zero.
             Implementations are required to check this field and make
             no assumptions about the format of unrecognized versions.

SIZE         The diameter of a sphere enclosing the described entity, in
             centimeters, expressed as a pair of four-bit unsigned
             integers, each ranging from zero to nine, with the most
             significant four bits representing the base and the second
             number representing the power of ten by which to multiply
             the base.  This allows sizes from 0e0 (<1cm) to 9e9
             (90,000km) to be expressed.  This representation was chosen
             such that the hexadecimal representation can be read by
             eye; 0x15 = 1e5.  Four-bit values greater than 9 are
             undefined, as are values with a base of zero and a non-zero
             exponent.

             Since 20000000m (represented by the value 0x29) is greater
             than the equatorial diameter of the WGS 84 ellipsoid
             (12756274m), it is therefore suitable for use as a
             "worldwide" size.

HORIZ PRE    The horizontal precision of the data, in centimeters,
             expressed using the same representation as SIZE.  This is
             the diameter of the horizontal "circle of error", rather
             than a "plus or minus" value.  (This was chosen to match
             the interpretation of SIZE; to get a "plus or minus" value,
             divide by 2.)

VERT PRE     The vertical precision of the data, in centimeters,
             expressed using the sane representation as for SIZE.  This
             is the total potential vertical error, rather than a "plus
             or minus" value.  (This was chosen to match the
             interpretation of SIZE; to get a "plus or minus" value,
             divide by 2.)  Note that if altitude above or below sea
             level is used as an approximation for altitude relative to
             the [WGS 84] ellipsoid, the precision value should be
             adjusted.

LATITUDE     The latitude of the center of the sphere described by the
             SIZE field, expressed as a 32-bit integer, most significant
             octet first (network standard byte order), in thousandths
             of a second of arc.  2^31 represents the equator; numbers
             above that are north latitude.

LONGITUDE    The longitude of the center of the sphere described by the
             SIZE field, expressed as a 32-bit integer, most significant
             octet first (network standard byte order), in thousandths
             of a second of arc, rounded away from the prime meridian.
             2^31 represents the prime meridian; numbers above that are
             east longitude.

ALTITUDE     The altitude of the center of the sphere described by the
             SIZE field, expressed as a 32-bit integer, most significant
             octet first (network standard byte order), in centimeters,
             from a base of 100,000m below the [WGS 84] reference
             spheroid used by GPS (semimajor axis a=6378137.0,
             reciprocal flattening rf=298.257223563).  Altitude above
             (or below) sea level may be used as an approximation of
             altitude relative to the the [WGS 84] spheroid, though due
             to the Earth's surface not being a perfect spheroid, there
             will be differences.  (For example, the geoid (which sea
             level approximates) for the continental US ranges from 10
             meters to 50 meters below the [WGS 84] spheroid.
             Adjustments to ALTITUDE and/or VERT PRE will be necessary
             in most cases.  The Defense Mapping Agency publishes geoid
             height values relative to the [WGS 84] ellipsoid.
        */
        [DNSField]
        public byte Version { get; set; }
        [DNSField]
        private byte size { get; set; }
        [DNSField]
        private byte horizontalPrecision { get; set; }
        [DNSField]
        private byte verticalPrecision { get; set; }

        [DNSField]
        private uint latitude { get; set; }
        [DNSField]
        private uint longitude { get; set; }
        [DNSField]
        private uint altitude { get; set; }

        private const double equatorLatitude = 2_147_483_648;
        private const double primeMeridian = 2_147_483_648;
        private const double altitudeZeroCorrection = 100_000_00;
        private const double arcSec = 1_296_000;
        private const double meter2Centimeter = 100;

        private double ExponentialValueConvert(byte value) => (value >> 4) * Math.Pow(10, value & 0xF);
        byte InverseExponentialValueConvert(double value)
        {
            int exponent = (int)Math.Floor(Math.Log10(value));
            int mantissa = (int)Math.Round(value /  Math.Pow(10, exponent));
            return (byte)((mantissa << 4) + exponent);
        }

        public double Size
        {
            get => ExponentialValueConvert(size);
            set => size = InverseExponentialValueConvert(value);
        }

        public double HorizontalPrecision {
            get => ExponentialValueConvert(horizontalPrecision) / meter2Centimeter;
            set => horizontalPrecision = InverseExponentialValueConvert(value * meter2Centimeter);
        }

        public double VerticalPrecision
        {
            get => ExponentialValueConvert(verticalPrecision) / meter2Centimeter;
            set => verticalPrecision = InverseExponentialValueConvert(value * meter2Centimeter);
        }

        public double Latitude
        {
            get => (latitude - equatorLatitude) / arcSec;
            set => latitude = (uint)((value * arcSec) + equatorLatitude);
        }

        public double Longitude
        {
            get => (longitude - primeMeridian) / arcSec;
            set => longitude = (uint)((value * arcSec) + primeMeridian);
        }

        public double Altitude
        {
            get => (altitude - altitudeZeroCorrection) / meter2Centimeter;
            set => altitude = (uint)((value * meter2Centimeter) + altitudeZeroCorrection);
        }


        public override string ToString() {
            return $"L:{Latitude} l:{Longitude} A:{Altitude} \n\t Size:{Size} Precision (h:{HorizontalPrecision}, v:{VerticalPrecision})" ;
        }
    }
}
