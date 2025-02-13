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

  int n = 0;
  int k = 0;
  std::cin >> n >> k;

  // DP requres n * k table, because of 1 <= k, n <= 2 * 10^5 it
  // inacceptable.
  //
  // Greedy solution is: Every time we pick or book which liked by
  // both or pick two books. So we need k iterations.
  priority_queue<int, vector<int>, std::greater<>> alice_like;
  priority_queue<int, vector<int>, std::greater<>> bob_like;
  priority_queue<int, vector<int>, std::greater<>> both_like;

  for (int i = 0; i < n; ++i) {
    int rt = 0;
    int al = 0;
    int bl = 0;

    std::cin >> rt >> al >> bl;
    if (al == 1 && bl == 1) {
      both_like.push(rt);
    } else if (al == 1) {
      alice_like.push(rt);
    } else if (bl == 1) {
      bob_like.push(rt);
    }
  }

  int res = 0;
  for (int i = 0; i < k; ++i) {
    if (both_like.empty() && (alice_like.empty() || bob_like.empty())) {
      res = -1;
      break;
    }

    if (both_like.empty()) {
      res += alice_like.top() + bob_like.top();
      alice_like.pop();
      bob_like.pop();
      continue;
    }

    if (bob_like.empty() || alice_like.empty()) {
      res += both_like.top();
      both_like.pop();
      continue;
    }

    if (both_like.top() < alice_like.top() + bob_like.top()) {
      res += both_like.top();
      both_like.pop();
      continue;
    }

    res += alice_like.top() + bob_like.top();
    alice_like.pop();
    bob_like.pop();
  }

  std::cout << res << "\n";

  return 0;
}
