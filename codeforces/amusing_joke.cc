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

bool yep(const string &first, const string &second, const string &pile) {
  unordered_map<char, int> m;
  for (char c : pile) {
    m[c]++;
  }

  for (char c : first + second) {
    if (!m[c]) return false;
    m[c]--;
  }

  for (const auto &pair : m) {
    if (pair.second) return false;
  }

  return true;
}

int main(int argc, char *argv[]) {
  string first, second, pile;
  std::cin >> first >> second >> pile;
  if (yep(first, second, pile)) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
