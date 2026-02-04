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

int main(int argc, char* argv[]) {
  string input;
  std::cin >> input;
  bool out = false;
  for (char c : input) {
    switch (c) {
      case 'H':
        out = true;
        break;
      case 'Q':
        out = true;
        break;
      case '9':
        out = true;
        break;
      default:
        continue;
    }
    if (out) break;
  }

  if (out)
    std::cout << "YES"
              << "\n";
  else
    std::cout << "NO"
              << "\n";

  return 0;
}
