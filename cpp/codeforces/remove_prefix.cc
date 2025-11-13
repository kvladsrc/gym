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
    unordered_map<int, int> m;
    int last_dup_pos = 0;
    bool found = false;

    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      if (m[buf]) {
        last_dup_pos = max(last_dup_pos, m[buf]);
        found = true;
      }
      m[buf] = i;
    }

    if (found) {
      std::cout << last_dup_pos << "\n";
    } else {
      std::cout << 0 << "\n";
    }
  }

  return 0;
}
