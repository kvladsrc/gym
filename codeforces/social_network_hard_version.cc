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

  size_t n, k;
  std::cin >> n >> k;

  // Lets use hash_map + linked list to get O(1) on check if element
  // on the screen and pop the last in case if is not.

  map<int, bool> id_on_screen;
  queue<int> ll;

  while ((n--) != 0) {
    int message = 0;
    std::cin >> message;

    if (id_on_screen[message]) {
      continue;
    }

    ll.push(message);
    id_on_screen[message] = true;

    if (ll.size() > k) {
      auto last = ll.front();
      ll.pop();
      id_on_screen[last] = false;
    }
  }

  // Reverse.
  std::stack<int> s;

  while (!ll.empty()) {
    s.push(ll.front());
    ll.pop();
  }

  std::cout << s.size() << "\n";
  while (!s.empty()) {
    std::cout << s.top() << " ";
    s.pop();
  }
  std::cout << "\n";

  return 0;
}
