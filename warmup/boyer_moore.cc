#include <cstddef>
#include <iostream>
#include <map>
#include <string>

using std::map;
using std::string;

static map<char, int> fill_last(string &patt) {
  map<char, int> table;
  for (size_t pos = 0; pos < patt.size(); ++pos) {
    table[patt[pos]] = pos + 1;
  }
  return table;
}

static bool boyer_moore(string &patt, string &s) {
  auto pos = fill_last(patt);
  size_t last = patt.size() - 1;

  while (last < s.size()) {
    if (patt.back() != s[last]) {
      last += patt.size() - pos[s[last]];
    } else {
      bool found = true;
      for (size_t i = 0; i < patt.size(); ++i) {
        if (patt[patt.size() - 1 - i] != s[last - i]) {
          found = false;
          break;
        }
      }

      if (found) {
        return true;
      }

      last++;
    }
  }

  return false;
}

int main(int /*argc*/, char * /*argv*/[]) {
  string s;
  string patt;
  std::cin >> patt >> s;
  if (boyer_moore(patt, s)) {
    std::cout << "FOUND" << "\n";
  } else {
    std::cout << "NOT FOUND" << "\n";
  }

  return 0;
}
