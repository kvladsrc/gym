#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
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
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, H, M;
    std::cin >> n >> H >> M;

    int sleep_time = H * 60 + M;

    int minutes_to_sleep = 24 * 60;
    while (n--) {
      int h, m;
      std::cin >> h >> m;

      int buf = (h * 60 + m);

      if (buf >= sleep_time) {
        minutes_to_sleep = min(minutes_to_sleep, buf - sleep_time);
      } else {
        int minutes_in_day = 24 * 60;
        minutes_to_sleep =
            min(minutes_to_sleep, minutes_in_day - (sleep_time - buf));
      }
    }

    std::cout << minutes_to_sleep / 60;
    std::cout << " ";
    std::cout << minutes_to_sleep % 60;
    std::cout << "\n";
  }

  return 0;
}
