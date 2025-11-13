#include <algorithm>
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
  int t;
  std::cin >> t;
  while (t--) {
    string time;
    std::cin >> time;

    int hours;
    hours = (time[0] - '0') * 10 + time[1] - '0';

    bool pm = hours / 12;
    hours %= 12;
    if (!hours) hours = 12;

    if (hours < 10) {
      std::cout << "0";
    }

    std::cout << hours << ":" << time[3] << time[4] << " ";
    if (pm) {
      std::cout << "PM"
                << "\n";
    } else {
      std::cout << "AM"
                << "\n";
    }
  }

  return 0;
}
