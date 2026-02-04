#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n, p;
  std::cin >> n >> p;

  vector<bool> stages(n);

  while (p) {
    int val;
    std::cin >> val;
    stages[val - 1] = true;
    p--;
  }

  int q;
  std::cin >> q;
  while (q) {
    int val;
    std::cin >> val;
    stages[val - 1] = true;
    q--;
  }

  bool res =
      std::all_of(stages.begin(), stages.end(), [](bool b) { return b; });

  if (res)
    std::cout << "I become the guy."
              << "\n";
  else
    std::cout << "Oh, my keyboard!"
              << "\n";

  return 0;
}
