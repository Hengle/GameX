from unittest import TestCase, main
from store import getPathByKey

# TestStoreAbandon
class TestvAbandon(TestCase):
    def test__init__(self):
        getPathByKey('Abandon:A')

# TestStoreBlizzard
class TestStoreBlizzard(TestCase):
    def test__init__(self):
        getPathByKey('Blizzard:A')

# TestStoreEpic
class TestStoreEpic(TestCase):
    def test__init__(self):
        getPathByKey('Epic:A')

# TestStoreGog
class TestStoreGog(TestCase):
    def test__init__(self):
        getPathByKey('Gog:A')

# TestStoreSteam
class TestStoreSteam(TestCase):
    def test__init__(self):
        getPathByKey('Steam:A')

# TestStoreUbisoft
class TestStoreUbisoft(TestCase):
    def test__init__(self):
        getPathByKey('Ubisoft:A')

# TestStoreUnknown
class TestStoreUnknown(TestCase):
    def test__init__(self):
        getPathByKey('Unknown:A')

if __name__ == "__main__":
    main(verbosity=1)