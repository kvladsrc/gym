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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  unordered_map<char, bool> m;

  for (int i = 0; i < n; ++i) {
    char c;
    std::cin >> c;
    m[std::tolower(c)] = true;
  }

  if (m.size() == 26) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
