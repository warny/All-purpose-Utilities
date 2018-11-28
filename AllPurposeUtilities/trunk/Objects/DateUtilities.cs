using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects
{
	public static class DateUtilities
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
