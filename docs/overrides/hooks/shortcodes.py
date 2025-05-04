from __future__ import annotations

import posixpath
import re

from mkdocs.config.defaults import MkDocsConfig
from mkdocs.structure.files import File, Files
from mkdocs.structure.pages import Page
from re import Match

# -----------------------------------------------------------------------------
# Hooks
# -----------------------------------------------------------------------------

__current_page = None
__current_config = None


# @todo
def on_page_markdown(markdown: str, *, page: Page, config: MkDocsConfig, files: Files):

    global __current_page
    global __current_config

    if __current_page != page:
        __current_page = page
        __current_config = None

    # Replace callback
    def replace(match: Match):
        type, args = match.groups()
        args = args.strip()

        match (type):
            case "flag":
                return flag(args, page, files)

            case "option":
                return option(args)

            case "setting":
                return setting(args)

            case "feature":
                return _badge_for_feature(args, page, files)

            case "utility":
                return _badge_for_utility(args, page, files)

            case "meta":
                return _metadata(args)

        # Otherwise, raise an error
        raise RuntimeError(f"Unknown shortcode: {type}")

    # Find and replace all external asset URLs in current page
    return re.sub(r"<!-- md:(\w+)(.*?) -->", replace, markdown, flags=re.I | re.M)


# -----------------------------------------------------------------------------
# Helper functions
# -----------------------------------------------------------------------------


# Create a flag of a specific type
def flag(args: str, page: Page, files: Files):
    type, *_ = args.split(" ", 1)
    match (type):
        case "wip":
            return _badge_for_work_in_progress(page, files)

        case "experimental":
            return _badge_for_experimental(page, files)

        case "required":
            return _badge_for_required(page, files)

    raise RuntimeError(f"Unknown type: {type}")


# Create a linkable option
def option(type: str):
    _, *_, name = re.split(r"[.:]", type)
    return f"[`{name}`](#+{type}){{ #+{type} }}\n\n"


# Create a linkable setting - @todo append them to the bottom of the page
def setting(type: str):
    _, *_, name = re.split(r"[.*]", type)
    return f"`{name}` {{ #{type} }}\n\n[{type}]: #{type}\n\n"


# -----------------------------------------------------------------------------


# Create badge
def _badgeify(icon: str, text: str = "", type: str = ""):
    classes = f"mdx-badge mdx-badge--{type}" if type else "mdx-badge"
    return "".join(
        [
            f'<span class="{classes}">',
            *([f'<span class="mdx-badge__icon">{icon}</span>'] if icon else []),
            *([f'<span class="mdx-badge__text">{text}</span>'] if text else []),
            f"</span>",
        ]
    )


def _badge_for_work_in_progress(page: Page, files: Files):
    icon = "material-progress-wrench"
    return _badgeify(icon=f':{icon}:{{ title="Work in progress" }}', type="warning")


# Create badge for feature
def _badge_for_feature(text: str, page: Page, files: Files):
    icon = "material-toggle-switch"
    return _badgeify(icon=f':{icon}:{{ title="Optional feature" }}', text=text)


# Create badge for utility
def _badge_for_utility(text: str, page: Page, files: Files):
    icon = "material-package-variant"
    return _badgeify(icon=f':{icon}:{{ title="Third-party utility" }}', text=text)


def _badge_for_required(page: Page, files: Files):
    icon = "material-alert"
    return _badgeify(icon=f':{icon}:{{ title="Required value" }}')


# Create badge for experimental flag
def _badge_for_experimental(page: Page, files: Files):
    icon = "material-flask-outline"
    return _badgeify(icon=f':{icon}:{{ title="Experimental" }}')


def _metadata(args: str) -> str:
    global __current_config

    parts = [part.strip() for part in args.split("|")]

    meta_key = parts[0] if len(parts) > 0 else "N/A"
    meta_value = parts[1] if len(parts) > 1 else None

    if meta_key == "config":
        __current_config = meta_value
        return ""  # f'<span class="metadata" title="The name for the configuration file explained in this article.">:material-file-settings: {meta_value}</span>'

    meta_key = _resolve_control_type(meta_key)

    return "".join(
        [
            "<span>",
            *(
                [
                    f'<span class="metadata" title="{meta_key[2] if len(meta_key) > 2 else "The control type."}">:{meta_key[1]}: {meta_key[0]}</span>'
                ]
                if meta_key
                else []
            ),
            *(
                [
                    f'<span class="metadata" title="The JSON key used to access this value in the configuration file."><span> | </span>:material-code-json: {__current_config} :material-arrow-right: {meta_value}</span>'
                ]
                if meta_value
                else []
            ),
            "</span>",
        ]
    )


def _resolve_control_type(arg: str) -> tuple[str, str, str]:
    match (arg):
        case "num":
            return ("Number selector", "material-pound")

        case "text":
            return ("Text box", "material-format-text")

        case "path":
            return ("Path selector", "octicons-rel-file-path-24")

        case "toggle":
            return ("Toggle", "material-toggle-switch-outline")

        case "page":
            return ("Page", "material-directions", "A redirection to a new page.") 

        case _:

            if not arg.startswith("::"):
                raise RuntimeError(f"Unknown metadata control type '{arg}'")
            