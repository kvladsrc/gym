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
    int n;
    std::cin >> n;
    unordered_map<int, int> table;
    int prev = 0;
    bool res = false;
    while (n--) {
      int buf;
      std::cin >> buf;
      if (res) continue;
      if (table[buf] == 1) {
        if (prev != 0 && prev != buf) {
          res = true;
        }
      } else if (table[buf] > 1) {
        res = true;
      }

      table[buf]++;
      prev = buf;
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
