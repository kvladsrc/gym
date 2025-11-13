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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int a1 = 0;
    int a2 = 0;
    int a4 = 0;
    int a5 = 0;

    std::cin >> a1 >> a2 >> a4 >> a5;

    set<int> a3;
    a3.insert(a1 + a2);
    a3.insert(a4 - a2);
    a3.insert(a5 - a4);

    std::cout << 4 - a3.size() << "\n";
  }

  return 0;
}
