#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  vector<int> lineland(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> lineland[i];
  }

  std::cout << abs(lineland[0] - lineland[1]) << " ";
  std::cout << abs(lineland[0] - lineland[n - 1]) << "\n";

  for (int i = 1; i < n - 1; ++i) {
    int min_dist, max_dist;
    min_dist = min(abs(lineland[i] - lineland[i - 1]),
                   abs(lineland[i] - lineland[i + 1]));
    max_dist =
        max(abs(lineland[i] - lineland[0]), abs(lineland[i] - lineland[n - 1]));
    std::cout << min_dist << " " << max_dist << "\n";
  }

  std::cout << abs(lineland[n - 2] - lineland[n - 1]) << " ";
  std::cout << abs(lineland[0] - lineland[n - 1]) << "\n";

  return 0;
}
