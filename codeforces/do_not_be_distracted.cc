#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;  // number of test cases
  std::cin >> t;

  while (t--) {
    int n;
    std::cin >> n;

    unordered_map<char, bool> m;
    bool prop = true;

    char prev;
    std::cin >> prev;
    m[prev] = true;
    n--;

    while (n--) {
      char c;
      std::cin >> c;

      // We cannot break a loop until reading all characters, even it
      // make no affect on answer.
      if (c == prev || !prop) continue;

      if (m[c]) {
        prop = false;
      }

      m[c] = true;
      prev = c;
    }

    if (prop) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
