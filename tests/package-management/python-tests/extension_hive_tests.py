import logging
import os
import shutil
import tempfile
from typing import cast
import uuid
from unittest.mock import Mock

import pytest
from apollo3zehn_package_management import PackageReference
from apollo3zehn_package_management._services import ExtensionHive


@pytest.mark.asyncio
async def can_instantiate_extensions_test():
    
    extension_folder_path = "tests/resources/test-extension"

    # create restore folder
    restore_root = os.path.join(tempfile.gettempdir(), f"PackageManagement.Tests.{uuid.uuid4()}")
    os.makedirs(restore_root)

    try:

        # load packages
        version = "v0.1.0"

        package_reference = PackageReference(
            provider="local",
            configuration={
                "path": extension_folder_path,
                "version": version,
                "entrypoint": "src",
                "import": "my_package.my_module"
            }
        )

        package_reference_map = {
            uuid.uuid4(): package_reference
        }

        hive = ExtensionHive[logging.Logger](restore_root, Mock())
        await hive.load_packages(package_reference_map)

        # instantiate
        loggerType = hive.get_extension_type("my_package.my_module.MyLogger")
        logger =  cast(logging.Logger, loggerType())

        with pytest.raises(Exception, match=r"c\[_\]"):
            assert logger.setLevel(0)

    finally:

        shutil.rmtree(restore_root)