#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string a;
    std::cin >> a;

    unordered_map<char, int> m;
    for (auto c : a) {
      m[c]++;
    }

    int both = 0;
    int singles = 0;
    for (auto p : m) {
      if (p.second > 1) {
        both++;
      } else {
        singles++;
      }
    }

    std::cout << both + (singles / 2) << "\n";
  }

  return 0;
}
