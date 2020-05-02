using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects
{
	public static class DateUtils
	{
		/// <summary>
		/// Renvoi le début de la période qui contient la date donnée
		/// </summary>
		/// <param name="datetime">Date de référence</param>
		/// <param name="period">Période</param>
		/// <param name="firstDayOfWeek">Jour du début de la semaine</param>
		/// <returns>Premier jour de la période</returns>
		public static DateTime StartOf( this DateTime datetime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek = DayOfWeek.Monday )
		{
			switch (period) {
				case PeriodTypeEnum.None:
					return datetime;
				case PeriodTypeEnum.Day:
					return datetime.Date;
				case PeriodTypeEnum.Week:
					int fdow = (int)firstDayOfWeek;
					int wod = (int)datetime.DayOfWeek;
					if (wod < fdow) {
						return datetime.Date.AddDays(-wod + fdow - 7);
					} else {
						return datetime.Date.AddDays(-wod + fdow);
					}
				case PeriodTypeEnum.Month:
					return datetime.Date.AddDays(-datetime.Day + 1);
				case PeriodTypeEnum.Quarter:
					DateTime sw = datetime.Date.AddDays(-datetime.Day + 1);
					sw = sw.AddMonths(-((sw.Month - 1) % 3));
					return sw;
				case PeriodTypeEnum.Year:
					return new DateTime(datetime.Year, 1, 1);
				default:
					return datetime;
			}
		}

		/// <summary>
		/// Renvoi la fin de la période qui contient la date donnée
		/// </summary>
		/// <param name="datetime">Date de référence</param>
		/// <param name="period">Période</param>
		/// <param name="firstDayOfWeek">Jour du début de la semaine</param>
		/// <returns>Dernier jour de la période</returns>
		public static DateTime EndOf( this DateTime datetime, PeriodTypeEnum periodType, DayOfWeek firstDayOfWeek = DayOfWeek.Monday )
		{
			switch (periodType) {
				case PeriodTypeEnum.None:
					return datetime;
				case PeriodTypeEnum.Day:
					return datetime.Date;
				case PeriodTypeEnum.Week:
					int fdow = (int)firstDayOfWeek;
					int wod = (int)datetime.DayOfWeek;
					if (wod < fdow) {
						return datetime.Date.AddDays(-wod + fdow - 1);
					} else {
						return datetime.Date.AddDays(-wod + fdow + 6);
					}
				case PeriodTypeEnum.Month:
					return datetime.Date.AddDays(-datetime.Day + 1).AddMonths(1).AddDays(-1);
				case PeriodTypeEnum.Quarter:
					DateTime sw = datetime.Date.AddDays(-datetime.Day + 1);
					sw = sw.AddMonths(3-((sw.Month - 1) % 3)).AddDays(-1);
					return sw;
				case PeriodTypeEnum.Year:
					return new DateTime(datetime.Year, 12, 31);
				default:
					return datetime;
			}
		}

		/// <summary>
		/// Date de référence unix
		/// </summary>
		public static DateTime UnixEpoch { get; } = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		/// <summary>
		/// Récupère la représentation UNIX de <paramref name="dateTime"/>
		/// </summary>
		/// <param name="dateTime"></param>
		/// <returns></returns>
		public static long UnixTimeStamp(this DateTime dateTime) => (long)((dateTime - UnixEpoch).TotalSeconds);

		/// <summary>
		/// Convertit le timestamp UNIX en date
		/// </summary>
		/// <param name="timestamp">Timestamp UNIX</param>
		/// <returns></returns>
		public static DateTime GetDateFromUnixTimeStamp(long timestamp) => UnixEpoch.AddSeconds(timestamp);

		/// <summary>
		/// Calcul de la date de paques
		/// </summary>
		/// <param name="year">Année de référence</param>
		/// <returns>Date de pâques</returns>
		public static DateTime ComputeEaster(int year)
		{
			//calcul du cycle de méton
			int metonCycle = year % 19;

			//calcul du siècle et du rang de l'année dans le siècle
			int century = year / 100;
			int yearRank = year % 100;
			//calcul siècle bissextile
			int century_s = century / 4;
			int century_t = century % 4;
			//calcul année bissextile
			int leapYear_b = yearRank / 4;
			int leapYear_d = yearRank % 4;

			//calcul du cycle de proemptose
			int proemptoseCycle = (century + 8) / 25;
			int proemptose = (century - proemptoseCycle + 1) / 3;

			//calcul épacte
			int epacte = (19 * metonCycle + century - century_s - proemptose + 15) % 30;


			//calcul lettre dominicale
			int sundayLetter = (2 * century_t + 2 * leapYear_b - epacte - leapYear_d + 32) % 7;

			//correction
			int correction = (metonCycle + 11 * epacte + 22 * sundayLetter) / 451;

			int easterDate = (epacte + sundayLetter - 7 * correction + 114);

			//calcul de la date de pâque
			return new DateTime(year, easterDate / 31, 1).AddDays(easterDate % 31);
		}

		/// <summary>
		/// Calcule la date de Pâques sur plusieurs années
		/// </summary>
		/// <param name="startYear"></param>
		/// <param name="endYear"></param>
		/// <returns></returns>
		public static IEnumerable<DateTime> ComputeEastern(int startYear, int endYear)
		{
			for (int year = startYear; year <= endYear; year++)
			{
				yield return ComputeEaster(year);					
			}
		}

	}

	/// <summary>
	/// Périodicité
	/// </summary>
	public enum PeriodTypeEnum
	{
		/// <summary>
		/// aucune
		/// </summary>
		None = 0,
		/// <summary>
		/// Quotidienne
		/// </summary>
		Day,
		/// <summary>
		/// Hebdomadaire
		/// </summary>
		Week,
		/// <summary>
		/// Mensuelle
		/// </summary>
		Month,
		/// <summary>
		/// Trimestrielle
		/// </summary>
		Quarter,
		/// <summary>
		/// Annuelle
		/// </summary>
		Year
	}
}
