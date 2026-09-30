
from logging import Logger

from .my_logger_utils import get_coffe_art


class MyLogger(Logger):
    
    def __init__(self):
        super().__init__("the-logger")

    def setLevel(self, level):
        coffee_art = get_coffe_art()
        raise Exception(coffee_art)