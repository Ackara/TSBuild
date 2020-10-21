using System.Collections.Generic;

namespace Acklann.TSBuild.Fake
{
	public class Library
	{
		public List<Book> Books { get; set; }

		public Book SelectedBook { get; set; }

		public string[] Coupons { get; set; }

		public Book[] FeaturedBooks { get; set; }
	}
}
