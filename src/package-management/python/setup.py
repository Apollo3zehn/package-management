import os
from pathlib import Path

import setuptools

source_dir = os.getcwd()

build_dir = "../../../artifacts/obj/python-package-management"
Path(build_dir).mkdir(parents=True, exist_ok=True)
os.chdir(build_dir)

with open(os.path.join(source_dir, "README.md"), "r") as fh:
    long_description = fh.read()

# setuptools normalizes SemVer version :-/ https://github.com/pypa/setuptools/issues/308
# The solution suggested there (from setuptools import sic, then call sic(version))
# is useless here because setuptools calls packaging.version.Version when .egg is created
# which again normalizes the version.

setuptools.setup(
    name="apollo3zehn-package-management",
    version=str(os.getenv("PYPI_VERSION")),
    description="A collection of types to easily implement a plugin system in your Python application. The source code of individual extensions can be located in remote git repositories or in a local folder structure.",
    long_description=long_description,
    long_description_content_type="text/markdown",
    author=str(os.getenv("AUTHORS")),
    url="https://github.com/Apollo3zehn/package-management",
    packages=[
        "apollo3zehn_package_management"
    ],
    project_urls={
        "Project": os.getenv("PACKAGEPROJECTURL"),
        "Repository": os.getenv("REPOSITORYURL"),
    },
    classifiers=[
        "Programming Language :: Python :: 3",
        "License :: OSI Approved :: MIT License",
        "Operating System :: OS Independent"
    ],
    license=str(os.getenv("PACKAGELICENSEEXPRESSION")),
    keywords="package management extensions plugins",
    platforms=[
        "any"
    ],
    package_dir={
        "apollo3zehn_package_management": os.path.join(source_dir, "apollo3zehn_package_management")
    },
    python_requires=">=3.10",
    install_requires=[]
)
