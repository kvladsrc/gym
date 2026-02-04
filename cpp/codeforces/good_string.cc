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

int ctoi(char c) { return c - '0'; }

int len(const vector<vector<size_t>>& table, int fd, int sd) {
  if (table[fd].empty() || table[sd].empty()) {
    return 0;
  }

  int res = 0;

  bool f_need = table[fd].front() < table[sd].front();
  size_t cur_idx = 0;
  size_t f_idx = 0;
  size_t s_idx = 0;

  // Can be optimized by binary search.
  while (true) {
    if (f_need) {
      while (f_idx < table[fd].size() && table[fd][f_idx] <= cur_idx) {
        f_idx++;
      }
      if (f_idx >= table[fd].size()) {
        break;
      }

      cur_idx = table[fd][f_idx];
    } else {
      while (s_idx < table[sd].size() && table[sd][s_idx] <= cur_idx) {
        s_idx++;
      }
      if (s_idx >= table[sd].size()) {
        break;
      }

      cur_idx = table[sd][s_idx];
    }

    res++;
    f_need = !f_need;
  }

  if (res % 2 && res > 1 && fd != sd) {
    res--;
  }

  return res;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;

    // Size 2 -- any string is good. Even size -- only row with
    // period 2. Let x != y.
    // ..x.y. -> ...x.y
    // ..x.y. -> .x.y..
    //
    // Odd size only if string string contains only one unique
    // character.
    //
    // Brute force approach. Lets check maximal substring for every
    // possible two digits.

    vector<vector<size_t>> table(10);
    for (size_t idx = 0; idx < s.size(); ++idx) {
      table[ctoi(s[idx])].push_back(idx + 1);
    }

    int mx = 0;
    for (int fd = 0; fd < 10; ++fd) {
      for (int sd = fd; sd < 10; ++sd) {
        auto local = len(table, fd, sd);
        mx = max(mx, local);
      }
    }
    std::cout << s.size() - mx << "\n";
  }

  return 0;
}
