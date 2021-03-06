﻿interface Book {
	id?: string;
	titel?: string;
}

interface Library {
	books?: Array<Book>;
	selectedBook?: Book;
	coupons?: Array<string>;
	featuredBooks?: Array<Book>;
}
