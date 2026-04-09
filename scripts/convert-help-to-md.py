#!/usr/bin/env python3
"""
Convert TopSolid online help HTM files to clean Markdown.

Usage:
    python convert-help-to-md.py <source_dir> <output_dir>

Recursively finds all .htm files in source_dir, converts them to Markdown,
and writes them to output_dir preserving the folder structure.

No external dependencies — uses only Python stdlib (html.parser).
"""

import sys
import os
import re
from html.parser import HTMLParser
from html import unescape


# Tags whose content should be completely stripped
STRIP_TAGS = {'script', 'style', 'form', 'iframe', 'select', 'option', 'noscript'}

# Block-level tags that produce line breaks
BLOCK_TAGS = {'p', 'div', 'br', 'hr', 'tr', 'li', 'blockquote',
              'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
              'table', 'thead', 'tbody', 'tfoot', 'td', 'th',
              'ul', 'ol', 'dl', 'dt', 'dd', 'pre', 'address',
              'section', 'article', 'nav', 'aside', 'header', 'footer', 'main'}

# CSS class -> Markdown heading level (TopSolid-specific)
TITLE_CLASSES = {
    'titre': 1,          # Main title
    'titre-rubrique': 2, # Section title
    'titre-bas': None,    # Footer separator — skip
    'separation': None,   # Separator line — skip
}


class HtmToMarkdown(HTMLParser):
    """
    Single-pass HTML parser that builds a list of Markdown fragments.
    """

    def __init__(self):
        super().__init__()
        self._fragments = []    # collected text fragments
        self._skip_depth = 0    # depth of nested strip-tags
        self._tag_stack = []    # stack of open tags
        self._list_stack = []   # stack of list types: 'ul' or 'ol'
        self._ol_counters = []  # counter for each nested ol
        self._in_bold = False
        self._heading_level = 0 # >0 when inside a heading tag
        self._title_override = 0 # heading level from CSS class
        self._pending_newlines = 0

    # ------------------------------------------------------------------
    # helpers
    # ------------------------------------------------------------------

    def _emit(self, text):
        if self._skip_depth > 0:
            return
        self._fragments.append(text)

    def _emit_newline(self, count=1):
        self._pending_newlines = max(self._pending_newlines, count)

    def _flush_newlines(self):
        if self._pending_newlines > 0:
            self._emit('\n' * self._pending_newlines)
            self._pending_newlines = 0

    def _css_class(self, attrs):
        for name, val in attrs:
            if name == 'class' and val:
                return val.strip().lower()
        return ''

    def _has_bold_style(self, attrs):
        """Check if inline style contains font-weight:bold."""
        for name, val in attrs:
            if name == 'style' and val and 'font-weight' in val and 'bold' in val:
                return True
        return False

    # ------------------------------------------------------------------
    # parser callbacks
    # ------------------------------------------------------------------

    def handle_starttag(self, tag, attrs):
        tag = tag.lower()
        self._tag_stack.append(tag)

        # Skip entire subtrees for script/style/form/iframe
        if tag in STRIP_TAGS:
            self._skip_depth += 1
            return
        if self._skip_depth > 0:
            return

        css = self._css_class(attrs)

        # Headings from HTML tags
        if tag in ('h1', 'h2', 'h3', 'h4', 'h5', 'h6'):
            level = int(tag[1])
            self._heading_level = level
            self._flush_newlines()
            self._emit_newline(2)
            self._flush_newlines()
            self._emit('#' * level + ' ')
            return

        # <p> with TopSolid CSS class for titles
        if tag == 'p' and css:
            for cls_key, level in TITLE_CLASSES.items():
                if cls_key in css:
                    if level is None:
                        # separator / footer — emit horizontal rule
                        self._emit_newline(2)
                        self._flush_newlines()
                        self._emit('---')
                        self._emit_newline(2)
                        self._title_override = -1  # flag to skip content
                        return
                    self._title_override = level
                    self._flush_newlines()
                    self._emit_newline(2)
                    self._flush_newlines()
                    self._emit('#' * level + ' ')
                    return

        # Block elements
        if tag in BLOCK_TAGS:
            if tag == 'br':
                self._emit_newline(1)
            elif tag == 'p':
                self._emit_newline(2)
            elif tag == 'tr':
                self._emit_newline(1)
            elif tag == 'li':
                self._flush_newlines()
                self._emit_newline(1)
                self._flush_newlines()
                indent = '  ' * max(0, len(self._list_stack) - 1)
                if self._list_stack and self._list_stack[-1] == 'ol':
                    idx = len(self._ol_counters) - 1
                    if idx >= 0:
                        self._ol_counters[idx] += 1
                        self._emit(f'{indent}{self._ol_counters[idx]}. ')
                    else:
                        self._emit(f'{indent}- ')
                else:
                    self._emit(f'{indent}- ')
            elif tag in ('ul', 'ol'):
                self._list_stack.append(tag)
                if tag == 'ol':
                    self._ol_counters.append(0)
                self._emit_newline(1)
            elif tag == 'td' or tag == 'th':
                # For layout tables, just add a space separator
                pass
            elif tag == 'hr':
                self._emit_newline(2)
                self._flush_newlines()
                self._emit('---')
                self._emit_newline(2)

        # Bold detection: <span> or <p> with bold style/class, or <b>/<strong>
        if tag in ('b', 'strong'):
            self._in_bold = True
            self._flush_newlines()
            self._emit('**')
        elif tag == 'span' and ('hcp5' in css or self._has_bold_style(attrs)):
            self._in_bold = True
            self._flush_newlines()
            self._emit('**')
        elif tag == 'em' or tag == 'i':
            self._flush_newlines()
            self._emit('*')

        # Links
        if tag == 'a':
            href = ''
            for name, val in attrs:
                if name == 'href' and val:
                    href = val
            if href and not href.startswith('#'):
                # Convert .htm references to .md
                href = re.sub(r'\.htm(#.*)?$', r'.md\1', href)
                self._flush_newlines()
                self._emit('[')
                # Store href to close later
                self._tag_stack[-1] = ('a', href)
            return

        # Images — just note alt text or skip
        if tag == 'img':
            alt = ''
            for name, val in attrs:
                if name == 'alt' and val:
                    alt = val
            # Skip decorative images (icons, banners)
            if alt:
                self._flush_newlines()
                self._emit(f'[{alt}]')

    def handle_endtag(self, tag):
        tag = tag.lower()

        # Pop tag stack
        popped = None
        if self._tag_stack:
            popped = self._tag_stack.pop()

        if tag in STRIP_TAGS:
            self._skip_depth = max(0, self._skip_depth - 1)
            return
        if self._skip_depth > 0:
            return

        # Headings
        if tag in ('h1', 'h2', 'h3', 'h4', 'h5', 'h6'):
            self._heading_level = 0
            self._emit_newline(2)
            return

        # Title override from CSS class
        if tag == 'p' and self._title_override != 0:
            if self._title_override > 0:
                self._emit_newline(2)
            self._title_override = 0
            return

        # Close block elements
        if tag == 'p':
            self._emit_newline(2)
        elif tag in ('ul', 'ol'):
            if self._list_stack:
                removed = self._list_stack.pop()
                if removed == 'ol' and self._ol_counters:
                    self._ol_counters.pop()
            self._emit_newline(1)
        elif tag == 'li':
            pass  # newline handled by next li or end of list
        elif tag == 'tr':
            self._emit_newline(1)
        elif tag == 'td' or tag == 'th':
            self._emit(' ')

        # Bold
        if tag in ('b', 'strong'):
            self._emit('**')
            self._in_bold = False
        elif tag == 'span' and self._in_bold:
            self._emit('**')
            self._in_bold = False
        elif tag in ('em', 'i'):
            self._emit('*')

        # Links
        if isinstance(popped, tuple) and popped[0] == 'a':
            href = popped[1]
            self._emit(f']({href})')

    def handle_data(self, data):
        if self._skip_depth > 0:
            return
        if self._title_override == -1:
            return  # skip content of separator elements

        # Collapse whitespace
        text = data
        # Replace common HTML entities that were already decoded
        text = text.replace('\r\n', '\n').replace('\r', '\n')
        # Collapse internal whitespace but preserve single newlines
        text = re.sub(r'[ \t]+', ' ', text)
        text = text.strip('\n')

        if not text or text == ' ':
            return

        self._flush_newlines()
        self._emit(text)

    def handle_entityref(self, name):
        if self._skip_depth > 0:
            return
        char = unescape(f'&{name};')
        if char == '\xa0':  # &nbsp;
            return  # skip non-breaking spaces used as padding
        self._flush_newlines()
        self._emit(char)

    def handle_charref(self, name):
        if self._skip_depth > 0:
            return
        char = unescape(f'&#{name};')
        if char == '\xa0':  # &#160;
            return
        self._flush_newlines()
        self._emit(char)

    def get_markdown(self):
        raw = ''.join(self._fragments)
        # Post-processing cleanup
        lines = raw.split('\n')
        cleaned = []
        for line in lines:
            stripped = line.strip()
            cleaned.append(stripped)

        result = '\n'.join(cleaned)

        # Remove empty bold markers
        result = re.sub(r'\*\*\s*\*\*', '', result)

        # Fix double-bold markers from nested bold detection
        result = re.sub(r'\*\*\*\*+', '**', result)

        # Remove orphan heading markers (# alone on a line)
        result = re.sub(r'^#{1,6}\s*$', '', result, flags=re.MULTILINE)

        # Remove orphan list markers (- alone, or N. alone)
        result = re.sub(r'^- $', '', result, flags=re.MULTILINE)
        result = re.sub(r'^\d+\. $', '', result, flags=re.MULTILINE)

        # Merge numbered list items split across lines:
        # "1.\n\nText" -> "1. Text"
        result = re.sub(r'^(\d+\.)\s*\n+\s*(\S)', r'\1 \2', result, flags=re.MULTILINE)

        # Merge bullet list items split across lines:
        # "- \n\nText" -> "- Text"  (with optional indent)
        result = re.sub(r'^((?:  )*-)\s*\n+\s*(\S)', r'\1 \2', result, flags=re.MULTILINE)

        # Clean up "N. -" patterns from <ol> wrapping <ul> (TopSolid quirk)
        result = re.sub(r'^\d+\. -\s*$', '', result, flags=re.MULTILINE)
        result = re.sub(r'^\d+\. - ', '- ', result, flags=re.MULTILINE)

        # Remove duplicate consecutive horizontal rules
        result = re.sub(r'(---\n)+---', '---', result)

        # Remove trailing spaces on lines
        result = re.sub(r' +\n', '\n', result)

        # Collapse 3+ consecutive blank lines to 2
        result = re.sub(r'\n{3,}', '\n\n', result)

        # Remove leading/trailing whitespace
        result = result.strip()

        # Remove title line if it's duplicated as first heading
        # (the <title> tag text appears before the # heading)
        lines = result.split('\n')
        if len(lines) >= 3:
            title_text = lines[0].strip()
            heading_text = lines[2].strip() if lines[1] == '' else ''
            if heading_text.startswith('# ') and heading_text[2:].strip() == title_text:
                lines = lines[2:]
                result = '\n'.join(lines)

        return result


def read_htm_file(filepath):
    """Read an HTM file, trying windows-1252 then utf-8."""
    for encoding in ('windows-1252', 'utf-8', 'latin-1'):
        try:
            with open(filepath, 'r', encoding=encoding) as f:
                return f.read()
        except (UnicodeDecodeError, UnicodeError):
            continue
    # Fallback: read with errors replaced
    with open(filepath, 'r', encoding='utf-8', errors='replace') as f:
        return f.read()


def convert_file(htm_path):
    """Convert a single HTM file to Markdown string."""
    html_content = read_htm_file(htm_path)
    parser = HtmToMarkdown()
    parser.feed(html_content)
    return parser.get_markdown()


def main():
    if len(sys.argv) < 3:
        print(f"Usage: {sys.argv[0]} <source_dir> <output_dir>")
        sys.exit(1)

    source_dir = os.path.normpath(sys.argv[1])
    output_dir = os.path.normpath(sys.argv[2])

    if not os.path.isdir(source_dir):
        print(f"Error: source directory not found: {source_dir}")
        sys.exit(1)

    # Collect all .htm files
    htm_files = []
    for root, dirs, files in os.walk(source_dir):
        for f in files:
            if f.lower().endswith('.htm') or f.lower().endswith('.html'):
                htm_files.append(os.path.join(root, f))

    htm_files.sort()
    total = len(htm_files)
    print(f"Found {total} HTM files in {source_dir}")

    converted = 0
    errors = 0
    total_input_size = 0
    total_output_size = 0

    for htm_path in htm_files:
        # Compute relative path and output path
        rel = os.path.relpath(htm_path, source_dir)
        md_rel = os.path.splitext(rel)[0] + '.md'
        md_path = os.path.join(output_dir, md_rel)

        # Ensure output directory exists
        os.makedirs(os.path.dirname(md_path), exist_ok=True)

        try:
            input_size = os.path.getsize(htm_path)
            total_input_size += input_size

            md_content = convert_file(htm_path)

            with open(md_path, 'w', encoding='utf-8') as f:
                f.write(md_content + '\n')

            output_size = len(md_content.encode('utf-8'))
            total_output_size += output_size
            converted += 1

        except Exception as e:
            print(f"  ERROR converting {rel}: {e}")
            errors += 1

    # Stats
    print(f"\n{'='*60}")
    print(f"Conversion complete!")
    print(f"  Files found:     {total}")
    print(f"  Converted:       {converted}")
    print(f"  Errors:          {errors}")
    print(f"  Input size:      {total_input_size / 1024:.1f} KB")
    print(f"  Output size:     {total_output_size / 1024:.1f} KB")
    print(f"  Compression:     {(1 - total_output_size / max(total_input_size, 1)) * 100:.1f}%")
    print(f"  Output dir:      {output_dir}")


if __name__ == '__main__':
    main()
