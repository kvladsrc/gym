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
    int n = 0;
    std::cin >> n;

    vector<int> vals(n);
    for (auto& i : vals) {
      std::cin >> i;
    }

    map<int, int> blue;
    map<int, int, std::greater<>> red;
    for (int i = 0; i < n; ++i) {
      char c;
      std::cin >> c;
      if (c == 'B') {
        blue[vals[i]]++;
      } else {
        red[vals[i]]++;
      }
    }

    // Lets try to construct array like BBBBRRRRR. Because, every
    // valid permutation can be transormed to such a form by
    // decreasing all B elements with indexies > number of B elements
    // and doing the same for red elements.
    //
    // We need to solve two subproblems. Lets Len_B be the number of
    // blue elements and Len_R -- number of red elements. We need to
    // check if we can obtain a permutation of Len_B elements from
    // blue elements and permutation of elements from Len_B + 1 to
    // Len_R numbers from red elements.

    bool res = true;

    int need = 1;
    for (auto p : blue) {
      while (p.second > 0) {
        if (p.first >= need) {
          need++;
          p.second--;
          continue;
        }

        res = false;
        break;
      }

      if (!res) {
        break;
      }
    }

    need = n;
    for (auto p : red) {
      while (p.second > 0) {
        if (p.first <= need) {
          need--;
          p.second--;
          continue;
        }

        res = false;
        break;
      }

      if (!res) {
        break;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
