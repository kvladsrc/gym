#include <iostream>
#include <string>

using std::string;

bool to_hello(string &a) {
  size_t cur = 0;
  string goal("hello");

  for (char c : a) {
    if (c == goal[cur]) {
      cur++;
      if (cur == goal.size())
        return true;
    }
  }

  return false;
}

int main(int argc, char *argv[]) {
  string input;
  std::cin >> input;
  if (to_hello(input)) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
