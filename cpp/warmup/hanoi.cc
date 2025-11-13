#include <iostream>
#include <string>

using std::string;

// Move n disks from first to third using second.
static int hanoi(int first, int second, int third, int n) {
  int res = 0;

  if (n == 0) {
    return res;
  }

  res += hanoi(first, third, second, n - 1);
  std::cout << "Move disk from: " << first << " to " << third << "\n";
  res++;
  res += hanoi(second, first, third, n - 1);

  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  auto moves = hanoi(1, 2, 3, n);

  std::cout << "Number of moves: " << moves << "\n";

  return 0;
}
