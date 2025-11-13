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
#include <utility>
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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    string a;
    string b;
    std::cin >> a >> b;

    /*
     * We can make every a_i be b_i. If operation on i does not give
     * the state then applying operation on 1 and then i
     * give. Solution if to keep track of operations to make suffix on
     * a be the same suffix on b. Problem is to keep track of elements
     * after operationsa. Instead of mutation of a lets use two
     * pointers for b from the last to first and second is for
     * a. After operation on i lets move the cursor to element which
     * is become i and the direction.
     */

    vector<int> res;
    size_t a_idx = n - 1;
    size_t a_first = 0;
    bool reversed = false;
    for (size_t idx = 0; idx < n; ++idx) {
      auto b_idx = n - 1 - idx;

      auto a_char = a[a_idx];
      if (reversed) {
        a_char = (a_char == '1' ? '0' : '1');
      }

      if (b[b_idx] != a_char) {
        auto a_first_char = a[a_first];
        if (reversed) {
          a_first_char = (a_first_char == '1' ? '0' : '1');
        }

        if (b[b_idx] == a_first_char) {
          res.push_back(1);
          a[a_first] = (a[a_first] == '1' ? '0' : '1');
          if (a_first != a_idx) {
            res.push_back(n - idx);
          }
        } else {
          res.push_back(n - idx);
        }

        reversed = !reversed;
        std::swap(a_first, a_idx);
      }

      if (!reversed) {
        a_idx--;
      } else {
        a_idx++;
      }
    }

    std::cout << res.size() << " ";
    for (auto o : res) {
      std::cout << o << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
