#include <algorithm>
#include <climits>
#include <cmath>
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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while ((t--) != 0) {
    int n;
    std::cin >> n;

    // After some experiments found that there are 2 types of
    // sequences: 2 -> 4 -> 8 > 6 -> 2 and 5 -> 0 -> 0. 1, 3, 7, 9
    // goes to 2 -> ... -> 2 sequene after one operation. But there
    // are infinitly many sequences ends by 0 (10, 20, 30, ...) and
    // two types of sequences with 2 ddd...x2: odd x and even x digit.
    //
    // Lets find out which type of sequcend each a_i belongs to. If
    // the set of the types A and |A| > 1 then the answer is "NO".

    set<int> s;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;

      // Exclude odd last digits.
      buf += buf % 10;

      auto last = buf % 10;
      auto pre = (buf / 10) % 10;

      switch (last) {
        case 2:
        case 4:
        case 6:
        case 8:
          if (pre % 2 == 0) {
            if (last == 6) {
              s.insert(12);
            } else {
              s.insert(2);
            }
          } else {
            if (last == 6) {
              s.insert(2);
            } else {
              s.insert(12);
            }
          }
          break;
        default:
          s.insert(buf);
      }
    }

    if (s.size() == 1) {
      std::cout << "YES" << "\n";
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}
