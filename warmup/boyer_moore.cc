#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

map<char, int> fill_last(string &patt) {
  map<char, int> table;
  for (size_t pos = 0; pos < patt.size(); ++pos) {
    table[patt[pos]] = pos + 1;
  }
  return table;
}

bool boyer_moore(string &patt, string &s) {
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

int main(int argc, char *argv[]) {
  string s, patt;
  std::cin >> patt >> s;
  if (boyer_moore(patt, s)) {
    std::cout << "FOUND" << "\n";
  } else {
    std::cout << "NOT FOUND" << "\n";
  }

  return 0;
}
