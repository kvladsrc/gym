#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

bool is_wrong(const string &input) {
  for (size_t i = 1; i < input.size(); ++i) {
    if (input[i] != std::toupper(input[i]))
      return false;
  }
  return true;
}

string decaps(const string &input) {
  string res;

  if (is_wrong(input)) {
    // First letter;
    if (input[0] == std::tolower(input[0])) {
      res += std::toupper(input[0]);
    } else {
      res += std::tolower(input[0]);
    }

    for (size_t i = 1; i < input.size(); ++i) {
      res += std::tolower(input[i]);
    }
  } else {
    res = input;
  }

  return res;
}

int main(int argc, char *argv[]) {
  string input;
  std::cin >> input;
  std::cout << decaps(input) << "\n";

  return 0;
}
