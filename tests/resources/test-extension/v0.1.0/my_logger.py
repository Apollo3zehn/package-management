
from logging import Logger
from art import art # pyright: ignore

class MyLogger(Logger):
    
    def __init__(self):
        super().__init__("the-logger")

    def setLevel(self, level): # pyright: ignore
        my_art = art("coffee")
        raise Exception(my_art)