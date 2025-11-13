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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, k;
  std::cin >> n >> k;

  vector<int> powers;
  int power = 1;

  while ((n + static_cast<int>(powers.size())) > k) {
    if (n <= 1) {
      std::cout << "NO" << "\n";
      return 0;
    }

    if (n % 2) {
      powers.push_back(power);
    }

    power *= 2;
    n /= 2;
  }

  int need = k - (n + static_cast<int>(powers.size()));
  while (n--) {
    if (need > 0) {
      if (power / 2 == 0) {
        std::cout << "NO" << "\n";
        return 0;
      }

      powers.push_back(power / 2);
      powers.push_back(power / 2);
      need--;
    } else {
      powers.push_back(power);
    }
  }

  std::cout << "YES" << "\n";
  for (auto p : powers) {
    std::cout << p << " ";
  }
  std::cout << endl;

  return 0;
}
