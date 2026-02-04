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
  int n, m;
  std::cin >> n >> m;

  int res = 0;
  int max_type = max(n, m);
  int min_type = min(n, m);

  int delta = max_type - min_type;
  res += min(delta, min_type);
  min_type -= res;
  max_type -= res * 2;

  if (min_type == max_type) {
    res += (min_type / 3) * 2;
    if (min_type % 3 == 2) {
      res++;
    }
  }

  std::cout << res << "\n";

  return 0;
}
