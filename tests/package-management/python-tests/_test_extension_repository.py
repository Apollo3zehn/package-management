import subprocess
from pathlib import Path


TEST_EXTENSION_TAGS = [
    "v0.1.0",
    "v1.0.0-alpha1+12345",
    "v1.0.0-beta1+12346",
    "v1.0.0-beta2+12347",
    "v1.0.1",
    "v1.1.1",
    "v2.0.0",
]

TEST_EXTENSION_PATH = Path("tests/resources/test-extension")


def get_test_extension_repository() -> str:
    _ensure_test_extension_repository()
    return TEST_EXTENSION_PATH.resolve().as_uri()


def _ensure_test_extension_repository():
    if (TEST_EXTENSION_PATH / ".git").exists():
        return

    _run_git("init")
    _run_git("add", ".")
    _run_git(
        "-c", "user.name=Package Management Tests",
        "-c", "user.email=package-management-tests@example.invalid",
        "commit", "-m", "Initial test extension"
    )

    for tag in TEST_EXTENSION_TAGS:
        _run_git("tag", tag)


def _run_git(*args: str):
    subprocess.run(
        ["git", *args],
        cwd=TEST_EXTENSION_PATH,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
