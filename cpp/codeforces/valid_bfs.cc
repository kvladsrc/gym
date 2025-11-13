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

using graph = vector<set<int>>;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  graph g(n);
  for (int i = 1; i < n; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].insert(b);
    g[b].insert(a);
  }

  queue<int> order;
  for (int i = 0; i < n; ++i) {
    int buf = 0;
    std::cin >> buf;
    buf--;
    order.push(buf);
  }

  if (order.front() != 0) {
    std::cout << "NO" << "\n";
    return 0;
  }

  order.pop();
  queue<int> q;
  q.push(0);
  bool res = true;

  while (!q.empty()) {
    auto cur = q.front();
    q.pop();

    int ns = g[cur].size();
    if (cur != 0) {
      ns--;
    }

    for (int i = 0; i < ns; ++i) {
      auto el = order.front();
      order.pop();
      if (g[cur].find(el) == g[cur].end()) {
        res = false;
        break;
      }
      q.push(el);
    }

    if (!res) {
      break;
    }
  }

  std::cout << (res ? "YES" : "NO") << "\n";

  return 0;
}
