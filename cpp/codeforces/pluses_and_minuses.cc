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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;

    int64_t res = 0;
    int64_t cur = 0;
    int balance = 0;
    int minimal = 0;
    for (auto c : s) {
      cur++;
      if (c == '+') {
        balance++;
      } else {
        balance--;
        if (minimal > balance) {
          minimal = balance;
          res += cur;
        }
      }
    }
    res += cur;
    std::cout << res << "\n";
  }

  return 0;
}
