import json

from helpers import CharacterTokenizer

from yaqs.chunking import chunk_document
from yaqs.cleaning import clean_document, strip_furniture, tidy
from yaqs.documents import Document, Page
from yaqs.ingest import import_book
from yaqs.store import KnowledgeBase


def book(pages):
    return Document("/book.jsonl", "hash", "Book", "Author", pages, "test")


def body(number):
    """Distinct text even after masking digits, like real paragraphs."""
    name = chr(65 + number % 26) + chr(65 + number // 26)
    return f"Paragraph {name} explains why enemy attacks need a readable wind-up."


def caption(number):
    return f"Figure {number}.1 " + body(number)[10:12] + " level."


def test_layout_blocks_lose_their_common_margin_but_keep_relative_indentation():
    layout = (
        "\ufeff282  ◾  Chapter 10   \n\n\n\n        A classic   example of attack.\n\n"
        "             12        # Get the direction\n"
        "             13        steering.linear = target -\n"
        "             14                          character\n"
    )
    assert tidy(layout, layout=True) == (
        "282 ◾ Chapter 10\n\nA classic example of attack.\n\n"
        "12 # Get the direction\n13 steering.linear = target -\n14 character"
    )
    assert tidy("def wander():\n\n\n\n    return  steering   \n", layout=False) == (
        "def wander():\n\n    return steering"
    )
    code = "def seek():\n    steering = new()\n        nested = 1"
    assert tidy(code, layout=True) == code
    assert tidy("  margin\n    indented", layout=True) == "margin\n  indented"


def test_running_headers_and_page_numbers_are_removed_and_recorded():
    pages = [
        Page(n, None, "", f"{n} ◾ Level Design\n{body(n)}\n{caption(n)}\n{n}")
        if n % 2
        else Page(n, None, "", f"Hell and Disneyland ◾ {n}\n{body(n)}")
        for n in range(10, 20)
    ]
    cleaned = strip_furniture(pages)
    assert [page.text for page in cleaned] == [
        f"{body(n)}\n{caption(n)}" if n % 2 else body(n) for n in range(10, 20)
    ]
    assert cleaned[1].provenance["removed_lines"] == ["11 ◾ Level Design", "11"]
    assert cleaned[0].provenance["removed_lines"] == ["Hell and Disneyland ◾ 10"]


def test_lines_repeated_far_apart_or_rarely_stay():
    pages = [Page(1, None, "", "Contents\nChapter 2: Combat\nChapter 3: Economy")]
    pages += [Page(n, None, "", body(n)) for n in range(2, 40)]
    pages[20] = Page(21, None, "", f"Chapter 2: Combat\n{body(21)}")
    pages[4] = Page(5, None, "", f"{body(5)}\nreturn steering")
    pages[30] = Page(31, None, "", f"{body(31)}\nreturn steering")
    for number in (22, 23, 24):  # a paragraph ending at three page bottoms in a row
        pages[number - 1] = Page(number, None, "", f"{body(number)}\nsystem.")
    pages[34] = Page(35, None, "", f"{body(35)}\nreturn steering")
    assert strip_furniture(pages) == pages


def test_unpaginated_text_is_never_treated_as_furniture():
    pages = [Page(0, None, "", "Header\n" + body(0) + "\nHeader")]
    assert strip_furniture(pages) == pages


def test_cleaning_stored_text_again_changes_nothing():
    pages = [
        Page(n, None, "", f"GAME FEEL\n   CHAPTER {n}\n\n   {body(n)}\n\n\n{n}", "pdf-text")
        for n in range(1, 12)
    ]
    once = clean_document(book(pages))
    assert once.pages[0].text == body(1)
    assert once.pages[0].provenance["removed_lines"] == ["GAME FEEL", "CHAPTER 1", "1"]
    assert clean_document(once) == once


def test_txt_headings_name_chapters_and_carry_over_pages():
    intro = "Patterns help. " * 8
    pages = [
        Page(1, None, "", f"# Command\n\n{intro}\n\n## Undo\n\n{intro}", "text"),
        Page(2, None, "", f"{intro}\n\n# Flyweight\n## Forest\n\n{intro}", "text"),
    ]
    chunks = list(chunk_document(book(pages), CharacterTokenizer(), size=4096, overlap=0))
    assert [(c.page_number, c.chapter, c.text.splitlines()[0]) for c in chunks] == [
        (1, "Command", "# Command"),
        (1, "Command › Undo", "## Undo"),
        (2, "Command › Undo", intro),
        # A heading without text joins the next section, whose path is more specific.
        (2, "Flyweight › Forest", "# Flyweight"),
    ]
    for chunk in chunks:
        assert chunk.text == pages[chunk.page_number - 1].text[chunk.start_char : chunk.end_char]


def test_short_trailing_section_joins_the_previous_one():
    text = "# Command\n\n" + "Patterns help. " * 8 + "\n\n## See Also\n\nObserver."
    chunks = list(chunk_document(book([Page(0, None, "", text, "text")]), CharacterTokenizer()))
    assert [(c.chapter, c.start_char, c.end_char) for c in chunks] == [("Command", 0, len(text))]


def test_headings_in_pdf_or_ocr_pages_do_not_change_their_chapter():
    text = "# Figure title\n\n" + "Patterns help. " * 8
    chunks = list(chunk_document(book([Page(5, None, "Combat", text)]), CharacterTokenizer()))
    assert [c.chapter for c in chunks] == ["Combat"]


def test_tiny_pages_stay_readable_but_get_no_chunk(tmp_path, encoder):
    source = tmp_path / "book.jsonl"
    rows = [
        {"page": 1, "text": "This page intentionally left blank"[:30], "extraction": "pdf-text"},
        {"page": 2, "text": body(2), "extraction": "pdf-text"},
    ]
    source.write_text("\n".join(json.dumps(row) for row in rows))

    import_book(tmp_path / "library.sqlite3", source, encoder)
    store = KnowledgeBase(tmp_path / "library.sqlite3")
    with store.connect() as db:
        assert [r[0] for r in db.execute("SELECT page_number FROM chunks")] == [2]
    assert store.page(1, 1)["text"] == "This page intentionally left b"


def test_import_stores_cleaned_text_and_removed_lines(tmp_path, encoder):
    source = tmp_path / "book.jsonl"
    rows = [
        {
            "page": n,
            "text": f"  {n}   Level Design\n\n      {body(n)}   \n",
            "extraction": "pdf-text",
            "chapter": "Combat",
            "page_label": str(n - 2),
        }
        for n in range(3, 9)
    ]
    source.write_text("\n".join(json.dumps(row) for row in rows))
    result = import_book(tmp_path / "library.sqlite3", source, encoder)
    store = KnowledgeBase(tmp_path / "library.sqlite3")
    page = store.page(result["book"]["id"], 4)
    assert page["text"] == body(4)
    assert page["provenance"]["removed_lines"] == ["4 Level Design"]
    assert (page["chapter"], page["page_label"]) == ("Combat", "2")
    hit = store.search("wind-up", mode="lexical", limit=1)[0]
    assert hit["text"] == body(3)
    assert json.loads(result["book"]["import_profile"])["text_format"] == 2
