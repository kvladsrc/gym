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
  unordered_map<char, bool> m;

  while (true) {
    char c;
    std::cin >> c;

    switch (c) {
      case '{':
        continue;
      case ',':
        continue;
      case ' ':
        continue;
      case '}':
        break;
      default:
        m[c] = true;
        continue;
    }
    break;
  }

  std::cout << m.size() << "\n";

  return 0;
}
