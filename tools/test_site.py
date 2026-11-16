import json
from html.parser import HTMLParser
from pathlib import Path
import struct
import unittest
from urllib.parse import urlsplit
import xml.etree.ElementTree as ET


SITE = Path(__file__).resolve().parents[1] / "site"
ORIGIN = "https://clipboardnext.com"


class PageHead(HTMLParser):
    def __init__(self, path):
        super().__init__()
        self.meta = {}
        self.canonicals = []
        self.title = ""
        self.structured_data = []
        self.in_head = False
        self.in_title = False
        self.in_json = False
        self.json_text = ""
        self.feed(path.read_text(encoding="utf-8"))

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "head":
            self.in_head = True
        if not self.in_head:
            return
        if tag == "meta":
            key = attrs.get("property", attrs.get("name"))
            if key:
                self.meta.setdefault(key, []).append(attrs.get("content", ""))
        elif tag == "link" and attrs.get("rel") == "canonical":
            self.canonicals.append(attrs.get("href"))
        elif tag == "title":
            self.in_title = True
        elif tag == "script" and attrs.get("type") == "application/ld+json":
            self.in_json = True
            self.json_text = ""

    def handle_endtag(self, tag):
        if tag == "head":
            self.in_head = False
        elif tag == "title":
            self.in_title = False
        elif tag == "script" and self.in_json:
            self.structured_data.append(json.loads(self.json_text))
            self.in_json = False

    def handle_data(self, data):
        if self.in_head and self.in_title:
            self.title += data
        if self.in_head and self.in_json:
            self.json_text += data


class SiteSEOTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pages = {
            path.relative_to(SITE).as_posix(): PageHead(path)
            for path in SITE.rglob("*.html")
            if path.name != "404.html"
        }

    def value(self, page, key):
        values = page.meta.get(key, [])
        self.assertEqual(len(values), 1, f"Expected exactly one {key}")
        self.assertTrue(values[0].strip(), f"Empty {key}")
        return values[0]

    def public_asset(self, url):
        parsed = urlsplit(url)
        self.assertEqual(f"{parsed.scheme}://{parsed.netloc}", ORIGIN)
        self.assertFalse(parsed.query or parsed.fragment)
        path = (SITE / parsed.path.lstrip("/")).resolve()
        self.assertTrue(path.is_relative_to(SITE))
        self.assertTrue(path.is_file(), f"Missing public asset: {url}")
        return path

    def test_search_and_open_graph_metadata(self):
        titles = set()
        descriptions = set()
        for filename, page in self.pages.items():
            with self.subTest(page=filename):
                url = ORIGIN + ("/" if filename == "index.html" else f"/{filename}")
                self.assertEqual(page.canonicals, [url])
                self.assertIn("Clipboard Next", page.title)
                self.assertLessEqual(len(page.title), 65)
                self.assertNotIn(page.title, titles)
                titles.add(page.title)
                description = self.value(page, "description")
                self.assertGreaterEqual(len(description), 50)
                self.assertLessEqual(len(description), 160)
                self.assertNotIn(description, descriptions)
                descriptions.add(description)
                self.assertEqual(self.value(page, "og:title"), page.title)
                self.assertEqual(self.value(page, "og:description"), description)
                self.assertEqual(self.value(page, "og:url"), url)
                self.assertEqual(self.value(page, "og:type"), "website")
                self.assertEqual(self.value(page, "og:site_name"), "Clipboard Next")
                self.assertEqual(self.value(page, "og:locale"), "en_US")
                robots = self.value(page, "robots")
                self.assertNotIn("noindex", robots)
                self.assertIn("max-image-preview:large", robots)

    def test_social_images_and_twitter_cards(self):
        for filename, page in self.pages.items():
            with self.subTest(page=filename):
                image_url = self.value(page, "og:image")
                image = self.public_asset(image_url)
                self.assertEqual(image.suffix, ".png")
                self.assertEqual(self.value(page, "og:image:type"), "image/png")
                data = image.read_bytes()
                self.assertEqual(data[:8], b"\x89PNG\r\n\x1a\n")
                self.assertEqual(data[12:16], b"IHDR")
                dimensions = struct.unpack(">II", data[16:24])
                self.assertEqual(dimensions, (1200, 630))
                self.assertEqual(
                    dimensions,
                    (
                        int(self.value(page, "og:image:width")),
                        int(self.value(page, "og:image:height")),
                    ),
                )
                self.assertLess(len(data), 1_000_000)
                alt = self.value(page, "og:image:alt")
                self.assertGreater(len(alt), 20)
                self.assertEqual(self.value(page, "twitter:card"), "summary_large_image")
                self.assertEqual(self.value(page, "twitter:title"), page.title)
                self.assertEqual(
                    self.value(page, "twitter:description"),
                    self.value(page, "description"),
                )
                self.assertEqual(self.value(page, "twitter:image"), image_url)
                self.assertEqual(self.value(page, "twitter:image:alt"), alt)

    def test_application_structured_data(self):
        blocks = self.pages["index.html"].structured_data
        self.assertEqual(len(blocks), 1)
        self.assertEqual(blocks[0]["@context"], "https://schema.org")
        graph = {item["@type"]: item for item in blocks[0]["@graph"]}
        website = graph["WebSite"]
        self.assertEqual(website["name"], "Clipboard Next")
        self.assertEqual(website["url"], f"{ORIGIN}/")
        app = graph["SoftwareApplication"]
        self.assertEqual(app["name"], "Clipboard Next")
        self.assertEqual(app["url"], f"{ORIGIN}/")
        self.assertEqual(app["applicationCategory"], "UtilitiesApplication")
        self.assertEqual(app["operatingSystem"], ["macOS 14+", "Windows 11"])
        self.assertEqual(app["offers"]["price"], "0")
        self.assertEqual(app["offers"]["priceCurrency"], "USD")
        self.public_asset(app["image"])

    def test_sitemap_covers_public_pages(self):
        root = ET.parse(SITE / "sitemap.xml").getroot()
        urls = [
            element.text
            for element in root.findall("{*}url/{*}loc")
        ]
        expected = {
            ORIGIN + ("/" if filename == "index.html" else f"/{filename}")
            for filename in self.pages
        }
        self.assertEqual(set(urls), expected)
        self.assertEqual(len(urls), len(expected))

    def test_robots_allows_crawling_and_advertises_sitemap(self):
        lines = (SITE / "robots.txt").read_text().splitlines()
        self.assertIn("User-agent: *", lines)
        self.assertIn("Allow: /", lines)
        self.assertNotIn("Disallow: /", lines)
        self.assertIn(f"Sitemap: {ORIGIN}/sitemap.xml", lines)

    def test_not_found_page_is_not_indexed(self):
        page = PageHead(SITE / "404.html")
        self.assertIn("noindex", self.value(page, "robots").split(","))
        self.assertEqual(page.canonicals, [])


if __name__ == "__main__":
    unittest.main()
